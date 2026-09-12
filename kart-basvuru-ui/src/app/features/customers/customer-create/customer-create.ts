import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormControl, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CustomerApiService } from '../customer-api.service';
import { CreateCustomerRequest } from '../customer.models';
import { AddressCatalogService, AddressOption } from '../../../core/services/address-catalog.service';
import {
  apartmentNoPattern, buildingNamePattern, buildingNoPattern, digitsOnly, floorPattern,
  isValidTurkishIdentityNumber, postalCodePattern, streetPattern, turkishIdentityNumberValidator,
} from '../../../core/validators/business-validators';

@Component({
  selector: 'app-customer-create',
  imports: [ReactiveFormsModule],
  templateUrl: './customer-create.html',
  styleUrl: './customer-create.scss',
})
export class CustomerCreate {
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly provinces = signal<AddressOption[]>([]);
  protected readonly districts = signal<AddressOption[]>([]);
  protected readonly neighborhoods = signal<AddressOption[]>([]);
  protected readonly streetOptions = signal<string[]>([]);
  protected readonly postalCodeStatus = signal<'official' | 'derived' | 'estimated' | ''>('');
  protected readonly addressLoading = signal(false);
  protected readonly externalCards = signal<{ bankName: string; maskedCardNumber: string; cardLimit: number }[]>([]);
  protected readonly showCancelConfirmation = signal(false);
  protected readonly maximumBirthDate = this.getMaximumBirthDate();
  protected readonly birthDay = new FormControl<number | null>(null);
  protected readonly birthMonth = new FormControl<number | null>(null);
  protected readonly birthYear = new FormControl<number | null>(null);
  protected readonly birthDays = Array.from({ length: 31 }, (_, index) => index + 1);
  protected readonly birthMonths = [
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
  ];
  protected readonly birthYears = Array.from(
    { length: 103 }, (_, index) => new Date().getFullYear() - 18 - index,
  );
  protected readonly phoneCountries = [
    { code: '+90', label: '🇹🇷 Türkiye (+90)', example: '5XXXXXXXXX' },
    { code: '+44', label: '🇬🇧 İngiltere (+44)', example: '7XXXXXXXXX' },
    { code: '+1', label: '🇺🇸 ABD (+1)', example: 'XXXXXXXXXX' },
    { code: '+49', label: '🇩🇪 Almanya (+49)', example: 'XXXXXXXXXX' },
  ];
  protected readonly bankOptions = [
    'Akbank', 'Albaraka Türk', 'DenizBank', 'Garanti BBVA', 'Halkbank', 'HSBC',
    'ING', 'Kuveyt Türk', 'QNB', 'TEB', 'Türkiye Finans', 'Türkiye İş Bankası',
    'VakıfBank', 'Yapı Kredi', 'Ziraat Bankası', 'Diğer',
  ];
  protected readonly form;

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly customerApiService: CustomerApiService,
    private readonly addressCatalogService: AddressCatalogService,
    private readonly router: Router,
    route: ActivatedRoute,
    destroyRef: DestroyRef,
  ) {
    this.form = formBuilder.nonNullable.group({
      nationalIdentityNumber: ['', [Validators.required, Validators.pattern(/^\d{11}$/), turkishIdentityNumberValidator()]],
      firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(60), Validators.pattern(/^[A-Za-zÇĞİÖŞÜçğıöşü]+(?: [A-Za-zÇĞİÖŞÜçğıöşü]+)*$/)]],
      lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(60), Validators.pattern(/^[A-Za-zÇĞİÖŞÜçğıöşü]+(?: [A-Za-zÇĞİÖŞÜçğıöşü]+)*$/)]],
      phoneCountryCode: ['+90', [Validators.required]],
      phoneNumber: ['', [Validators.required, this.phoneValidator()]],
      emailAddress: ['', [Validators.required, Validators.email]],
      birthDate: ['', [Validators.required, this.adultBirthDateValidator()]],
      gender: ['', [Validators.required]],
      educationLevel: ['', [Validators.required]],
      occupation: ['', [Validators.required]],
      employmentStatus: ['', [Validators.required]],
      occupationOther: [''],
      city: ['', [Validators.required]],
      district: [{ value: '', disabled: true }, [Validators.required]],
      neighborhood: [{ value: '', disabled: true }, [Validators.required]],
      street: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120), Validators.pattern(streetPattern)]],
      avenue: ['', [Validators.maxLength(120), Validators.pattern(streetPattern)]],
      building: ['', [Validators.required, Validators.minLength(1), Validators.maxLength(20), Validators.pattern(buildingNoPattern)]],
      buildingName: ['', [Validators.maxLength(60), Validators.pattern(buildingNamePattern)]],
      apartment: ['', [Validators.maxLength(20), Validators.pattern(apartmentNoPattern)]],
      floor: ['', [Validators.pattern(floorPattern)]],
      postalCode: ['', [Validators.required, Validators.pattern(postalCodePattern)]],
      monthlyNetIncome: [null as unknown as number, [Validators.required, Validators.min(1)]],
      otherBankTotalCardLimit: [0, [Validators.required, Validators.min(0)]],
      kvkkConsentGranted: [false, [Validators.requiredTrue]],
      smsConsentGranted: [false],
      emailConsentGranted: [false],
      confirmed: [false, [Validators.requiredTrue]],
    });
    const identity = route.snapshot.queryParamMap.get('nationalIdentityNumber');
    if (identity && /^\d{11}$/.test(identity)) {
      this.form.controls.nationalIdentityNumber.setValue(identity);
      this.refreshExternalRiskProfile();
    }
    this.addressCatalogService.getProvinces().subscribe({
      next: (items) => this.provinces.set(items),
      error: () => this.errorMessage.set('İl listesi alınamadı. İnternet bağlantısını kontrol edin.'),
    });
    this.form.controls.phoneCountryCode.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => this.form.controls.phoneNumber.updateValueAndValidity());
    this.form.controls.occupation.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(value => {
      const other = this.form.controls.occupationOther;
      other.setValidators(value === 'Diğer' ? [Validators.required, Validators.minLength(2), Validators.maxLength(80)] : []);
      if (value !== 'Diğer') other.reset('', { emitEvent: false });
      other.updateValueAndValidity({ emitEvent: false });
    });
    this.form.controls.city.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe((cityName) => this.loadDistricts(cityName));
    this.form.controls.district.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe((districtName) => this.loadNeighborhoods(districtName));
    this.form.controls.neighborhood.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe((name) => this.loadNeighborhoodAddressData(name));
    [this.birthDay, this.birthMonth, this.birthYear].forEach(control =>
      control.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => this.syncBirthDate()));
  }

  private syncBirthDate(): void {
    const day = this.birthDay.value;
    const month = this.birthMonth.value;
    const year = this.birthYear.value;
    if (!day || !month || !year) {
      this.form.controls.birthDate.setValue('');
      return;
    }
    const date = new Date(Date.UTC(year, month - 1, day));
    const isValid = date.getUTCFullYear() === year
      && date.getUTCMonth() === month - 1
      && date.getUTCDate() === day;
    this.form.controls.birthDate.setValue(isValid
      ? `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}` : '');
    this.form.controls.birthDate.markAsTouched();
  }

  protected save(): void {
    this.errorMessage.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      queueMicrotask(() => document.querySelector<HTMLElement>('form .ng-invalid:not(form)')?.focus());
      return;
    }

    this.isSaving.set(true);
    const {
      confirmed: _confirmed,
      birthDate,
      occupationOther,
      street,
      avenue,
      building,
      buildingName: _buildingName,
      apartment,
      floor,
      postalCode,
      ...values
    } = this.form.getRawValue();
    const otherBankCards = this.externalCards().map(card => ({ ...card }));
    const request: CreateCustomerRequest = {
      ...values,
      occupation: values.occupation === 'Diğer' ? occupationOther.trim() : values.occupation,
      birthDate: birthDate || null,
      address: this.composedAddress(),
      street: street.trim(),
      avenue: avenue.trim() || null,
      buildingNo: building.trim(),
      apartmentNo: apartment.trim() || null,
      floor: floor.trim() || null,
      postalCode: postalCode.trim(),
      otherBankTotalCardLimit: otherBankCards.reduce((sum, card) => sum + card.cardLimit, 0),
      otherBankCards,
    };
    this.customerApiService.create(request).subscribe({
      next: (customer) => void this.router.navigate(['/officer/customers', customer.id], {
        state: { customerCreated: true },
      }),
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.error?.detail ?? error.error?.title ?? 'Müşteri kaydedilemedi. API bağlantısını kontrol edin.');
        this.isSaving.set(false);
      },
    });
  }

  protected sanitizeIdentity(): void {
    const value = digitsOnly(this.form.controls.nationalIdentityNumber.value, 11);
    this.form.controls.nationalIdentityNumber.setValue(value, { emitEvent: false });
    this.refreshExternalRiskProfile();
  }

  protected normalizeName(control: AbstractControl<string>): void {
    const value = control.value.replace(/\s+/g, ' ').trim();
    const normalized = value.toLocaleLowerCase('tr-TR').replace(/(^|\s)(\p{L})/gu,
      (_match, prefix: string, letter: string) => `${prefix}${letter.toLocaleUpperCase('tr-TR')}`);
    control.setValue(normalized);
  }

  protected sanitizePhone(): void {
    const maximum = this.form.controls.phoneCountryCode.value === '+49' ? 11 : 10;
    this.form.controls.phoneNumber.setValue(
      digitsOnly(this.form.controls.phoneNumber.value, maximum), { emitEvent: false });
  }

  protected normalizeEmail(): void {
    this.form.controls.emailAddress.setValue(
      this.form.controls.emailAddress.value.trim().toLocaleLowerCase('tr-TR'), { emitEvent: false });
  }

  protected cancel(): void {
    if (!this.form.dirty) { void this.router.navigate(['/officer/customers/search']); return; }
    this.showCancelConfirmation.set(true);
  }

  protected confirmCancel(): void {
    this.showCancelConfirmation.set(false);
    void this.router.navigate(['/officer/customers/search']);
  }

  protected educationOccupationError(): string {
    const education = this.form.controls.educationLevel.value;
    const occupation = this.form.controls.occupation.value;
    const professional = ['Mühendis', 'Öğretmen', 'Doktor', 'Avukat', 'Eczacı'];
    const lowEducation = ['İlköğretim', 'Lise', 'Ön Lisans'].includes(education);
    return professional.includes(occupation) && lowEducation
      ? `${occupation} mesleği için eğitim düzeyi en az Lisans olmalıdır.` : '';
  }

  protected clearZero(control: AbstractControl<number>): void {
    if (control.value === 0) control.setValue(null as unknown as number);
  }

  protected otherBankTotal(): number {
    return this.externalCards().reduce((sum, card) => sum + card.cardLimit, 0);
  }

  protected composedAddress(): string {
    const values = this.form.getRawValue();
    const avenue = values.avenue.trim();
    const building = values.building.trim() ? `Bina No: ${values.building.trim()}` : '';
    const buildingName = values.buildingName.trim() ? `Bina: ${values.buildingName.trim()}` : '';
    const apartment = values.apartment.trim() ? `Daire: ${values.apartment.trim()}` : '';
    const floor = values.floor.trim() ? `Kat: ${values.floor.trim()}` : '';
    const locality = [values.neighborhood, values.district, values.city]
      .map(value => value.trim()).filter(Boolean).join(' / ');
    return [values.street.trim(), avenue, buildingName, building, floor, apartment, values.postalCode.trim(), locality]
      .filter(Boolean).join(', ');
  }

  protected prototypeCreditScore(): number {
    const income = Number(this.form.controls.monthlyNetIncome.value || 0);
    const birthDate = this.form.controls.birthDate.value ? new Date(`${this.form.controls.birthDate.value}T00:00:00`) : null;
    const age = birthDate ? Math.max(18, new Date().getFullYear() - birthDate.getFullYear()) : 18;
    return Math.max(0, Math.min(1900, Math.round(700 + Math.min(650, income / 100)
      + Math.min(220, (age - 18) * 8) - (income ? Math.min(600, this.otherBankTotal() / income * 160) : 500))));
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('tr-TR', {
      style: 'currency',
      currency: 'TRY',
      maximumFractionDigits: 0,
    }).format(value);
  }

  protected phonePlaceholder(): string {
    return this.phoneCountries.find((item) =>
      item.code === this.form.controls.phoneCountryCode.value)?.example ?? 'Telefon numarası';
  }

  private loadDistricts(cityName: string): void {
    this.districts.set([]);
    this.neighborhoods.set([]);
    this.streetOptions.set([]);
    this.postalCodeStatus.set('');
    this.form.controls.postalCode.setValue('');
    this.form.controls.district.reset('');
    this.form.controls.neighborhood.reset('');
    this.form.controls.neighborhood.disable();
    const province = this.provinces().find((item) => item.name === cityName);
    if (!province) {
      this.form.controls.district.disable();
      return;
    }
    this.addressLoading.set(true);
    this.addressCatalogService.getDistricts(province.id).subscribe({
      next: (items) => {
        this.districts.set(items);
        this.form.controls.district.enable();
        this.addressLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('İlçe listesi alınamadı.');
        this.addressLoading.set(false);
      },
    });
  }

  private loadNeighborhoods(districtName: string): void {
    this.neighborhoods.set([]);
    this.streetOptions.set([]);
    this.postalCodeStatus.set('');
    this.form.controls.postalCode.setValue('');
    this.form.controls.neighborhood.reset('');
    const district = this.districts().find((item) => item.name === districtName);
    if (!district) {
      this.form.controls.neighborhood.disable();
      return;
    }
    this.addressLoading.set(true);
    this.addressCatalogService.getNeighborhoods(district.id).subscribe({
      next: (items) => {
        this.neighborhoods.set(items);
        this.form.controls.neighborhood.enable();
        this.addressLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Mahalle listesi alınamadı.');
        this.addressLoading.set(false);
      },
    });
  }

  private loadNeighborhoodAddressData(neighborhoodName: string): void {
    this.form.controls.street.reset('');
    this.streetOptions.set([]);
    const neighborhood = this.neighborhoods().find(item => item.name === neighborhoodName);
    this.form.controls.postalCode.setValue(neighborhood?.postalCode ?? '');
    this.postalCodeStatus.set(neighborhood?.postalCodeStatus ?? '');
    if (!neighborhood) return;
    this.addressLoading.set(true);
    this.addressCatalogService.getStreets(neighborhood.id).subscribe({
      next: items => {
        this.streetOptions.set(items.map(item => item.name));
        this.addressLoading.set(false);
        if (!items.length) this.errorMessage.set('Seçilen mahalle için güncel cadde/sokak kaydı bulunamadı.');
      },
      error: () => {
        this.addressLoading.set(false);
        this.errorMessage.set('Ulusal cadde/sokak kataloğu yüklenemedi. API bağlantısını kontrol edin.');
      },
    });
  }

  private phoneValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const digits = String(control.value ?? '').replace(/\D/g, '');
      const code = this.form?.controls.phoneCountryCode?.value ?? '+90';
      const valid = code === '+90' ? /^5\d{9}$/.test(digits)
        : code === '+44' ? /^7\d{9}$/.test(digits)
        : code === '+1' ? /^[2-9]\d{9}$/.test(digits)
        : code === '+49' ? /^\d{10,11}$/.test(digits)
        : false;
      return valid ? null : { phoneFormat: true };
    };
  }

  private getMaximumBirthDate(): string {
    const date = new Date();
    date.setFullYear(date.getFullYear() - 18);
    date.setDate(date.getDate() - 1);
    return date.toISOString().slice(0, 10);
  }

  private adultBirthDateValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) return null;
      const birthDate = new Date(`${control.value}T00:00:00`);
      const maximum = new Date(`${this.getMaximumBirthDate()}T23:59:59`);
      const minimum = new Date();
      minimum.setFullYear(minimum.getFullYear() - 120);
      return Number.isNaN(birthDate.getTime()) || birthDate > maximum || birthDate < minimum
        ? { adultAge: true } : null;
    };
  }

  private refreshExternalRiskProfile(): void {
    const identity = this.form.controls.nationalIdentityNumber.value;
    if (!isValidTurkishIdentityNumber(identity)) {
      this.externalCards.set([]);
      this.form.controls.otherBankTotalCardLimit.setValue(0);
      return;
    }
    this.customerApiService.getExternalRiskProfile(identity).subscribe({
      next: (profile) => {
        if (this.form.controls.nationalIdentityNumber.value !== identity) return;
        this.externalCards.set(profile.cards.map(card => ({
          bankName: card.bankName,
          maskedCardNumber: card.cardLastFourDigits,
          cardLimit: card.cardLimit,
        })));
        this.form.controls.otherBankTotalCardLimit.setValue(profile.otherBankTotalCardLimit);
      },
      error: () => {
        if (this.form.controls.nationalIdentityNumber.value !== identity) return;
        this.externalCards.set([]);
        this.form.controls.otherBankTotalCardLimit.setValue(0);
        this.errorMessage.set('Dış risk profili alınamadı. API bağlantısını kontrol edin.');
      },
    });
  }
}
