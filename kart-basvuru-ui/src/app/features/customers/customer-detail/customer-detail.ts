import { Location } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CustomerApiService } from '../customer-api.service';
import { Customer, CustomerApplicationSummary, CustomerCardSummary } from '../customer.models';
import { AddressCatalogService, AddressOption } from '../../../core/services/address-catalog.service';
import { Consent, PlatformApiService, SupplementaryApplication } from '../../../core/services/platform-api.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.scss',
})
export class CustomerDetail implements OnInit {
  protected readonly customer = signal<Customer | null>(null);
  protected readonly cards = signal<CustomerCardSummary[]>([]);
  protected readonly applications = signal<CustomerApplicationSummary[]>([]);
  protected readonly supplementaryCards = signal<SupplementaryApplication[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly wasCreated = history.state.customerCreated === true;
  protected readonly isEditing = signal(false);
  protected readonly updateMessage = signal('');
  protected readonly provinces = signal<AddressOption[]>([]);
  protected readonly districts = signal<AddressOption[]>([]);
  protected readonly neighborhoods = signal<AddressOption[]>([]);
  protected readonly verificationChannel = signal<'Phone' | 'Email' | null>(null);
  protected readonly verificationDestination = signal('');
  protected readonly demoCode = signal('');
  protected readonly verificationSecondsLeft = signal(0);
  protected readonly showStatusConfirmation = signal(false);
  protected readonly consents = signal<Consent[]>([]);
  protected readonly kkbAnalysis = signal<any>(null);
  protected readonly activeCardCount = computed(() => this.cards().filter(x => ['Active', '2'].includes(x.status)).length);
  protected readonly openApplicationCount = computed(() => this.applications().filter(x => ['Pending', 'Revision'].includes(x.status)).length);
  protected readonly customerWarnings = computed(() => {
    const customer = this.customer(); if (!customer) return [];
    const warnings: string[] = [];
    if (!customer.isActive) warnings.push('Müşteri pasif; yeni ürün işlemleri kapalıdır.');
    if (!customer.isProfileComplete) warnings.push(`Eksik profil: ${customer.missingProfileFields.join(', ')}`);
    if (!customer.isPhoneVerified) warnings.push('Telefon doğrulanmamış.');
    if (!customer.isEmailVerified) warnings.push('E-posta doğrulanmamış.');
    if (customer.availableCardLimit <= 0) warnings.push('Kullanılabilir kart limiti bulunmuyor.');
    if (this.openApplicationCount()) warnings.push(`${this.openApplicationCount()} açık başvuru sonuçlanmayı bekliyor.`);
    return warnings;
  });
  protected readonly verificationCode = new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\d{6}$/)] });
  protected readonly maximumBirthDate = this.getMaximumBirthDate();
  private verificationTimer: ReturnType<typeof setInterval> | null = null;
  protected readonly editForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-zÇĞİÖŞÜçğıöşü ]{2,50}$/)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-zÇĞİÖŞÜçğıöşü ]{2,50}$/)] }),
    phoneCountryCode: new FormControl('+90', { nonNullable: true, validators: [Validators.required] }),
    phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{7,11}$/)] }),
    emailAddress: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    birthDate: new FormControl('', { nonNullable: true, validators: [Validators.required, control => control.value && control.value <= this.maximumBirthDate ? null : { underage: true }] }),
    gender: new FormControl('Belirtilmedi', { nonNullable: true, validators: [Validators.required] }),
    educationLevel: new FormControl('Belirtilmedi', { nonNullable: true, validators: [Validators.required] }),
    occupation: new FormControl('Belirtilmedi', { nonNullable: true, validators: [Validators.required] }),
    employmentStatus: new FormControl('Çalışıyor', { nonNullable: true, validators: [Validators.required] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    district: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    neighborhood: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    address: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(10)] }),
    monthlyNetIncome: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly customerApiService: CustomerApiService,
    private readonly addressCatalogService: AddressCatalogService,
    private readonly platformApi: PlatformApiService,
    protected readonly auth: AuthService,
    private readonly location: Location,
    destroyRef: DestroyRef,
  ) {
    destroyRef.onDestroy(() => this.stopVerificationTimer());
    this.editForm.controls.city.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(city => this.loadDistricts(city));
    this.editForm.controls.district.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(district => this.loadNeighborhoods(district));
    this.editForm.controls.phoneCountryCode.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => {
        this.editForm.controls.phoneNumber.reset('');
        this.updatePhoneValidation();
      });
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(id) || id <= 0) {
      this.errorMessage.set('Geçersiz müşteri numarası.');
      this.isLoading.set(false);
      return;
    }

    this.customerApiService.getDetailById(id).subscribe({
      next: (detail) => {
        const customer = detail.customer;
        this.customer.set(customer);
        this.cards.set(detail.cards);
        this.applications.set(detail.applications);
        this.editForm.setValue({
          firstName: customer.firstName, lastName: customer.lastName,
          phoneCountryCode: customer.phoneCountryCode, phoneNumber: customer.phoneNumber,
          emailAddress: customer.emailAddress, birthDate: customer.birthDate ?? '', gender: customer.gender,
          educationLevel: customer.educationLevel, occupation: customer.occupation, employmentStatus: customer.employmentStatus, city: customer.city,
          district: customer.district, neighborhood: customer.neighborhood, address: customer.address,
          monthlyNetIncome: customer.monthlyNetIncome,
        }, { emitEvent: false });
        this.loadInitialAddresses(customer);
        this.updatePhoneValidation();
        this.platformApi.customerConsents(customer.id).subscribe(data => this.consents.set(data));
        this.platformApi.kkb(customer.id).subscribe(data => this.kkbAnalysis.set(data));
        this.platformApi.supplementary().subscribe({
          next: rows => this.supplementaryCards.set(rows.filter(row => row.holderCustomerId === customer.id)),
          error: () => this.supplementaryCards.set([]),
        });
        this.isLoading.set(false);
      },
      error: () => { this.errorMessage.set('Müşteri bulunamadı veya API bağlantısı kurulamadı.'); this.isLoading.set(false); },
    });
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 }).format(value);
  }

  protected rolePrefix(): string { return this.auth.currentUser()?.role === 'Manager' ? '/manager' : '/officer'; }
  protected goBack(): void { this.location.back(); }

  protected phonePlaceholder(): string {
    return ({ '+90': '5XX XXX XX XX', '+44': '7XXX XXXXXX', '+1': 'XXX XXX XXXX', '+49': '1XX XXXXXXXX' } as Record<string, string>)[this.editForm.controls.phoneCountryCode.value] ?? 'Telefon numarası';
  }

  protected sanitizePhone(event: Event): void {
    const input = event.target as HTMLInputElement;
    const maximum = this.editForm.controls.phoneCountryCode.value === '+49' ? 11 : 10;
    const value = input.value.replace(/\D/g, '').slice(0, maximum);
    input.value = value;
    this.editForm.controls.phoneNumber.setValue(value);
  }

  protected save(): void {
    const customer = this.customer();
    if (!customer || this.editForm.invalid) { this.editForm.markAllAsTouched(); return; }
    const values = this.editForm.getRawValue();
    this.customerApiService.update(customer.id, {
      ...values,
      birthDate: values.birthDate || null,
      otherBankTotalCardLimit: customer.otherBankTotalCardLimit,
      otherBankCards: customer.otherBankCards.map(card => ({
        bankName: card.bankName, maskedCardNumber: card.maskedCardNumber, cardLimit: card.cardLimit,
      })),
    }).subscribe({
      next: (updated) => { this.customer.set(updated); this.isEditing.set(false); this.updateMessage.set('Müşteri bilgileri güncellendi.'); },
      error: (response) => this.errorMessage.set(response.error?.detail ?? 'Müşteri bilgileri güncellenemedi.'),
    });
  }

  protected startVerification(channel: 'Phone' | 'Email'): void {
    const customer = this.customer();
    if (!customer) return;
    this.customerApiService.requestContactVerification(customer.id, channel).subscribe({
      next: result => {
        this.verificationChannel.set(channel);
        this.verificationDestination.set(result.maskedDestination);
        this.demoCode.set(result.demoCode);
        this.verificationCode.reset('');
        this.startVerificationTimer();
        this.updateMessage.set(`Doğrulama kodu ${result.maskedDestination} adresine gönderilmek üzere üretildi.`);
      },
      error: response => this.errorMessage.set(response.error?.detail ?? 'Doğrulama kodu üretilemedi.'),
    });
  }

  protected confirmVerification(): void {
    const customer = this.customer();
    const channel = this.verificationChannel();
    if (!customer || !channel || this.verificationCode.invalid || this.verificationSecondsLeft() <= 0) {
      this.verificationCode.markAsTouched();
      if (this.verificationSecondsLeft() <= 0) this.errorMessage.set('Doğrulama kodunun süresi doldu. Yeni kod gönderin.');
      return;
    }
    this.customerApiService.verifyContact(customer.id, channel, this.verificationCode.value).subscribe({
      next: (updated) => {
        this.customer.set(updated);
        this.updateMessage.set(`${channel === 'Phone' ? 'Telefon' : 'E-posta'} doğrulandı.`);
        this.verificationChannel.set(null);
        this.demoCode.set('');
        this.stopVerificationTimer();
      },
      error: (response) => this.errorMessage.set(response.error?.detail ?? 'Doğrulama tamamlanamadı.'),
    });
  }

  protected toggleStatus(): void {
    const customer = this.customer();
    if (!customer) return;
    if (customer.isActive) { this.showStatusConfirmation.set(true); return; }
    this.changeCustomerStatus(true);
  }

  protected confirmStatusChange(): void {
    this.showStatusConfirmation.set(false);
    this.changeCustomerStatus(false);
  }

  protected verificationTimeText(): string {
    const seconds = this.verificationSecondsLeft();
    return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
  }

  private changeCustomerStatus(isActive: boolean): void {
    const customer = this.customer();
    if (!customer) return;
    this.customerApiService.setStatus(customer.id, isActive).subscribe({
      next: (updated) => {
        this.customer.set(updated);
        this.updateMessage.set(updated.isActive ? 'Müşteri aktife alındı.' : 'Müşteri pasife alındı.');
      },
      error: (response) => this.errorMessage.set(response.error?.detail ?? 'Müşteri durumu değiştirilemedi.'),
    });
  }

  private startVerificationTimer(): void {
    this.stopVerificationTimer();
    this.verificationSecondsLeft.set(300);
    this.verificationTimer = setInterval(() => {
      const next = Math.max(0, this.verificationSecondsLeft() - 1);
      this.verificationSecondsLeft.set(next);
      if (next === 0) this.stopVerificationTimer();
    }, 1000);
  }

  private stopVerificationTimer(): void {
    if (this.verificationTimer) clearInterval(this.verificationTimer);
    this.verificationTimer = null;
  }

  protected setConsent(type: string, granted: boolean): void {
    const customer = this.customer(); if (!customer) return;
    this.platformApi.setConsent(customer.id, type, granted).subscribe({
      next: () => { this.platformApi.customerConsents(customer.id).subscribe(data => this.consents.set(data)); this.updateMessage.set(`${type} izni güncellendi ve geçmişe kaydedildi.`); },
      error: response => this.errorMessage.set(response.error?.detail ?? 'Açık rıza kaydedilemedi.'),
    });
  }

  protected latestConsent(type: string): Consent | null {
    return this.consents().find(x => x.consentType === type) ?? null;
  }

  protected maskIdentity(value: string): string {
    return `${value.slice(0, 2)}*******${value.slice(-2)}`;
  }

  protected statusLabel(value: string): string {
    return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda',
      Inactive: 'Pasif', Active: 'Aktif', Blocked: 'Blokeli', Expired: 'Süresi Dolmuş', Cancelled: 'Kapatılmış',
      '0': 'Pasif', '1': 'Pasif', '2': 'Aktif', '3': 'Blokeli', '4': 'Süresi Dolmuş', '5': 'Kapatılmış' } as Record<string, string>)[value] ?? value;
  }

  protected cardStatusLabel(cardStatus: string, applicationStatus: string): string {
    if (applicationStatus !== 'Approved') return ({ Pending:'Başvuru bekliyor', Rejected:'Başvuru reddedildi' } as Record<string,string>)[applicationStatus] ?? applicationStatus;
    return this.statusLabel(cardStatus);
  }

  private loadInitialAddresses(customer: Customer): void {
    this.addressCatalogService.getProvinces().subscribe({
      next: provinces => {
        this.provinces.set(provinces);
        const province = provinces.find(item => item.name === customer.city);
        if (!province) return;
        this.addressCatalogService.getDistricts(province.id).subscribe({
          next: districts => {
            this.districts.set(districts);
            const district = districts.find(item => item.name === customer.district);
            if (!district) return;
            this.addressCatalogService.getNeighborhoods(district.id).subscribe({
              next: neighborhoods => this.neighborhoods.set(neighborhoods),
            });
          },
        });
      },
      error: () => this.errorMessage.set('Adres kataloğu yüklenemedi.'),
    });
  }

  private loadDistricts(cityName: string): void {
    this.districts.set([]);
    this.neighborhoods.set([]);
    this.editForm.controls.district.setValue('', { emitEvent: false });
    this.editForm.controls.neighborhood.setValue('', { emitEvent: false });
    const province = this.provinces().find(item => item.name === cityName);
    if (!province) return;
    this.addressCatalogService.getDistricts(province.id).subscribe({
      next: items => this.districts.set(items),
      error: () => this.errorMessage.set('İlçe listesi yüklenemedi.'),
    });
  }

  private loadNeighborhoods(districtName: string): void {
    this.neighborhoods.set([]);
    this.editForm.controls.neighborhood.setValue('', { emitEvent: false });
    const district = this.districts().find(item => item.name === districtName);
    if (!district) return;
    this.addressCatalogService.getNeighborhoods(district.id).subscribe({
      next: items => this.neighborhoods.set(items),
      error: () => this.errorMessage.set('Mahalle listesi yüklenemedi.'),
    });
  }

  private getMaximumBirthDate(): string {
    const date = new Date();
    date.setFullYear(date.getFullYear() - 18);
    date.setDate(date.getDate() - 1);
    return date.toISOString().slice(0, 10);
  }

  private updatePhoneValidation(): void {
    const pattern = ({
      '+90': /^5\d{9}$/,
      '+44': /^7\d{9}$/,
      '+1': /^[2-9]\d{9}$/,
      '+49': /^\d{10,11}$/,
    } as Record<string, RegExp>)[this.editForm.controls.phoneCountryCode.value] ?? /^$/;
    this.editForm.controls.phoneNumber.setValidators([Validators.required, Validators.pattern(pattern)]);
    this.editForm.controls.phoneNumber.updateValueAndValidity({ emitEvent: false });
  }
}
