import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CustomerApiService } from '../../customers/customer-api.service';
import { Customer, CustomerAddress } from '../../customers/customer.models';
import { CardApplicationApiService } from '../card-application-api.service';
import { CardApplication, CardType } from '../card-application.models';
import { AddressCatalogService, AddressOption } from '../../../core/services/address-catalog.service';
import { DuplicateApplication, PlatformApiService } from '../../../core/services/platform-api.service';
import { forkJoin, of } from 'rxjs';
import { addressNamePattern, apartmentNoPattern, buildingNamePattern, buildingNoPattern, floorPattern, postalCodePattern, streetPattern } from '../../../core/validators/business-validators';
import { SupplementaryApplications } from '../../platform/supplementary-applications';

@Component({
  selector: 'app-application-create',
  imports: [ReactiveFormsModule, RouterLink, SupplementaryApplications],
  templateUrl: './application-create.html',
  styleUrl: './application-create.scss',
})
export class ApplicationCreate implements OnInit {
  protected readonly applicationKind = signal<'Primary' | 'Supplementary'>('Primary');
  private readonly branchCatalog = [
    { city: 'İstanbul', district: 'Ümraniye', name: 'Finanskent Şubesi' },
    { city: 'İstanbul', district: 'Ataşehir', name: 'Ataşehir Şubesi' },
    { city: 'İstanbul', district: 'Kadıköy', name: 'Kadıköy Şubesi' },
    { city: 'İstanbul', district: 'Üsküdar', name: 'Üsküdar Şubesi' },
    { city: 'İstanbul', district: 'Beşiktaş', name: 'Levent Şubesi' },
    { city: 'Ankara', district: 'Çankaya', name: 'Çankaya Şubesi' },
    { city: 'Ankara', district: 'Yenimahalle', name: 'Batıkent Şubesi' },
    { city: 'İzmir', district: 'Konak', name: 'Konak Şubesi' },
    { city: 'İzmir', district: 'Karşıyaka', name: 'Karşıyaka Şubesi' },
    { city: 'Bursa', district: 'Nilüfer', name: 'Nilüfer Şubesi' },
    { city: 'Antalya', district: 'Muratpaşa', name: 'Muratpaşa Şubesi' },
  ];
  protected readonly step = signal<1 | 2 | 3 | 4>(1);
  protected readonly query = new FormControl('', { nonNullable: true, validators: Validators.required });
  protected readonly searchResults = signal<Customer[]>([]);
  protected readonly selectedCustomer = signal<Customer | null>(null);
  protected readonly cardTypes = signal<CardType[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly createdApplication = signal<CardApplication | null>(null);
  protected readonly duplicateWarning = signal<DuplicateApplication | null>(null);
  protected readonly duplicateWarningDismissed = signal(false);
  protected readonly limitNoticeDismissed = signal(false);
  protected readonly notice = signal('');
  protected readonly draftInfo = signal<{ customerId: number; savedAt: string } | null>(this.readDraftInfo());
  protected readonly selectedDocuments = signal<Record<string, File>>({});
  protected readonly provinces = signal<AddressOption[]>([]);
  protected readonly districts = signal<AddressOption[]>([]);
  protected readonly neighborhoods = signal<AddressOption[]>([]);
  protected readonly streetOptions = signal<string[]>([]);
  protected readonly addressLoading = signal(false);
  protected readonly postalCodeStatus = signal<'official' | 'derived' | 'estimated' | ''>('');
  protected readonly selectedProductName = signal('');
  protected readonly selectedNetworkName = signal<'Visa' | 'Mastercard' | 'TROY'>('Visa');
  protected readonly networkOptions = [
    { value: 'Visa' as const, label: 'Visa', logo: '/assets/card-networks/visa.svg' },
    { value: 'Mastercard' as const, label: 'Mastercard', logo: '/assets/card-networks/mastercard.svg' },
    { value: 'TROY' as const, label: 'TROY', logo: '/assets/card-networks/troy.svg' },
  ];
  protected readonly productOptions = computed(() => {
    const seen = new Set<string>();
    return this.cardTypes().filter(item => !seen.has(item.productName) && !!seen.add(item.productName));
  });
  protected readonly savedAddresses = signal<CustomerAddress[]>([]);
  protected readonly phoneCountries = [
    { code: '+90', label: 'Türkiye (+90)' },
    { code: '+44', label: 'İngiltere (+44)' },
    { code: '+1', label: 'ABD (+1)' },
    { code: '+49', label: 'Almanya (+49)' },
  ];
  protected readonly applicationForm;
  protected readonly availableLimit = computed(() => {
    const customer = this.selectedCustomer();
    return customer ? customer.availableCardLimit : 0;
  });
  protected readonly selectedCardType = computed(() =>
    this.cardTypes().find((item) => item.id === this.applicationForm.controls.cardTypeId.value) ?? null,
  );
  protected readonly requestedLimitBelowProductMinimum = computed(() => {
    const minimum = this.selectedCardType()?.minimumLimit;
    return minimum !== null && minimum !== undefined
      && this.applicationForm.controls.requestedLimit.value < minimum;
  });
  protected readonly requestedLimitAboveCalculatedMaximum = computed(() =>
    this.applicationForm.controls.requestedLimit.value > this.availableLimit());

  constructor(
    formBuilder: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly customerApiService: CustomerApiService,
    private readonly applicationApiService: CardApplicationApiService,
    private readonly addressCatalogService: AddressCatalogService,
    private readonly platformApi: PlatformApiService,
    destroyRef: DestroyRef,
  ) {
    this.applicationForm = formBuilder.nonNullable.group({
      cardTypeId: [0, [Validators.required, Validators.min(1)]],
      requestedLimit: [0, [Validators.required, Validators.min(1)]],
      deliveryMethod: ['RegisteredAddress' as 'RegisteredAddress' | 'DifferentAddress' | 'Branch', [Validators.required]],
      deliveryAddress: ['', [Validators.maxLength(500)]],
      deliveryCity: [''],
      deliveryDistrict: [''],
      deliveryNeighborhood: [''],
      deliveryStreet: ['', [Validators.pattern(streetPattern), Validators.maxLength(120)]],
      deliveryAvenue: ['', [Validators.pattern(buildingNamePattern), Validators.maxLength(120)]],
      deliveryBuildingNo: ['', [Validators.pattern(buildingNoPattern), Validators.maxLength(20)]],
      deliveryApartmentNo: ['', [Validators.pattern(apartmentNoPattern), Validators.maxLength(20)]],
      deliveryFloor: ['', [Validators.pattern(floorPattern)]],
      deliveryPostalCode: ['', [Validators.pattern(postalCodePattern)]],
      selectedSavedAddressId: [0],
      addressName: ['', [Validators.pattern(addressNamePattern), Validators.maxLength(40)]],
      saveDeliveryAddress: [false],
      deliveryRecipientName: [''],
      deliveryPhoneCountryCode: ['+90'],
      deliveryPhone: ['', [Validators.pattern(/^\d{10,11}$/)]],
      deliveryBranch: [''],
      statementPreference: ['Email' as 'Email' | 'Paper' | 'Mobile', [Validators.required]],
      statementDay: [14, [Validators.required, Validators.pattern(/^(7|14|21|28)$/)]],
      contactlessEnabled: [false],
      internetShoppingEnabled: [false],
      automaticLimitIncreaseEnabled: [false],
      identityDocumentConfirmed: [false, [Validators.requiredTrue]],
      incomeDocumentConfirmed: [false, [Validators.requiredTrue]],
      residenceDocumentConfirmed: [false, [Validators.requiredTrue]],
      duplicateWarningAcknowledged: [false],
      applicationNote: ['', [Validators.maxLength(500)]],
      confirmed: [false, [Validators.requiredTrue]],
    });
    this.applicationForm.controls.deliveryCity.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(city => this.loadDistricts(city));
    this.applicationForm.controls.deliveryDistrict.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(district => {
        this.applicationForm.controls.deliveryBranch.setValue('');
        this.loadNeighborhoods(district);
      });
    this.applicationForm.controls.deliveryNeighborhood.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(neighborhood => this.loadNeighborhoodAddressData(neighborhood));
    this.applicationForm.controls.deliveryMethod.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(method => {
        if (method !== 'RegisteredAddress') {
          this.applicationForm.patchValue({
            deliveryCity: '', deliveryDistrict: '', deliveryNeighborhood: '', deliveryBranch: '',
            deliveryAddress: '', deliveryStreet: '', deliveryAvenue: '', deliveryBuildingNo: '',
            deliveryApartmentNo: '', deliveryFloor: '', deliveryPostalCode: '', selectedSavedAddressId: 0,
            addressName: '', saveDeliveryAddress: false,
          }, { emitEvent: false });
          this.districts.set([]);
          this.neighborhoods.set([]);
          this.streetOptions.set([]);
          this.postalCodeStatus.set('');
        } else {
          const defaultAddress = this.savedAddresses().find(item => item.isDefault) ?? this.savedAddresses()[0];
          if (defaultAddress) {
            this.selectSavedAddress(defaultAddress.id);
          } else {
            this.applicationForm.controls.deliveryAddress.setValue(this.selectedCustomer()?.address ?? '');
          }
        }
      });
    this.addressCatalogService.getProvinces().subscribe({
      next: items => this.provinces.set(items),
      error: () => this.errorMessage.set('Adres kataloğu yüklenemedi.'),
    });
  }

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('type')?.toLowerCase() === 'supplementary'
      || this.route.snapshot.queryParamMap.has('primaryCardId')) {
      this.applicationKind.set('Supplementary');
    }
    this.applicationApiService.getCardTypes().subscribe({
      next: (types) => this.cardTypes.set(types),
      error: () => this.errorMessage.set('Kart tipleri yüklenemedi. API bağlantısını kontrol edin.'),
    });

    const customerId = Number(this.route.snapshot.queryParamMap.get('customerId'));
    if (Number.isInteger(customerId) && customerId > 0) {
      this.loadCustomer(customerId);
    }
  }

  protected selectApplicationKind(kind: 'Primary' | 'Supplementary'): void {
    this.applicationKind.set(kind);
    if (kind === 'Primary') void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  protected searchCustomer(): void {
    this.errorMessage.set('');
    if (this.query.invalid) {
      this.query.markAsTouched();
      return;
    }
    this.isLoading.set(true);
    this.customerApiService.search(this.query.value.trim()).subscribe({
      next: (customers) => {
        this.searchResults.set(customers);
        this.hasSearched.set(true);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Müşteri sorgulanamadı. API bağlantısını kontrol edin.');
        this.hasSearched.set(true);
        this.isLoading.set(false);
      },
    });
  }

  protected chooseCustomer(customer: Customer): void {
    if (!customer.isActive) {
      this.errorMessage.set('Pasif müşteri için kart başvurusu oluşturulamaz.');
      return;
    }
    if (!customer.isProfileComplete) {
      void this.router.navigate(['/officer/customers', customer.id], {
        queryParams: { returnUrl: '/officer/applications/new' },
      });
      return;
    }
    this.selectedCustomer.set(customer);
    this.searchResults.set([]);
    this.errorMessage.set('');
    this.applicationForm.patchValue({
      deliveryAddress: customer.address || 'Kayıtlı müşteri adresi',
      deliveryRecipientName: `${customer.firstName} ${customer.lastName}`,
      deliveryPhoneCountryCode: customer.phoneCountryCode,
      deliveryPhone: customer.phoneNumber,
    });
    this.customerApiService.getAddresses(customer.id).subscribe({
      next: addresses => {
        this.savedAddresses.set(addresses);
        const defaultAddress = addresses.find(item => item.isDefault) ?? addresses[0];
        if (defaultAddress) this.selectSavedAddress(defaultAddress.id);
      },
      error: () => this.savedAddresses.set([]),
    });
    this.step.set(2);
  }

  protected selectSavedAddress(addressId: number): void {
    const address = this.savedAddresses().find(item => item.id === Number(addressId));
    this.applicationForm.controls.selectedSavedAddressId.setValue(address?.id ?? 0, { emitEvent: false });
    if (!address) return;
    this.applicationForm.patchValue({
      deliveryAddress: address.fullAddress,
      deliveryCity: address.city,
      deliveryDistrict: address.district,
      deliveryNeighborhood: address.neighborhood,
    }, { emitEvent: false });
  }

  protected chooseCardType(cardType: CardType): void {
    this.selectedProductName.set(cardType.productName);
    this.selectedNetworkName.set(cardType.network);
    this.duplicateWarningDismissed.set(false);
    this.limitNoticeDismissed.set(false);
    this.applicationForm.controls.cardTypeId.setValue(cardType.id);
    const suggestedLimit = Math.min(this.availableLimit(), cardType.maximumLimit ?? this.availableLimit());
    this.applicationForm.controls.requestedLimit.setValue(suggestedLimit);
    this.applicationForm.controls.requestedLimit.setValidators([
      Validators.required, Validators.min(cardType.minimumLimit ?? 1),
    ]);
    this.applicationForm.controls.requestedLimit.updateValueAndValidity();
    const customer = this.selectedCustomer();
    this.duplicateWarning.set(null);
    this.applicationForm.controls.duplicateWarningAcknowledged.setValue(false);
    if (customer) this.platformApi.duplicateCheck(customer.id, cardType.id).subscribe({
      next: result => this.duplicateWarning.set(result.hasDuplicate ? result : null),
    });
  }

  protected chooseProduct(productName: string): void {
    this.selectedProductName.set(productName);
    this.selectProductNetworkCombination();
  }

  protected chooseNetwork(network: 'Visa' | 'Mastercard' | 'TROY'): void {
    this.selectedNetworkName.set(network);
    this.selectProductNetworkCombination();
  }

  protected networkLogo(): string {
    return this.networkOptions.find(item => item.value === this.selectedNetworkName())?.logo ?? '';
  }

  protected goToSummary(): void {
    this.errorMessage.set('');
    if (this.applicationForm.invalid) {
      this.applicationForm.markAllAsTouched();
      queueMicrotask(() => document.querySelector<HTMLElement>('.application-fields .ng-invalid')?.focus());
      return;
    }
    if (!['Identity', 'Income', 'Residence'].every(type => this.selectedDocuments()[type])) {
      this.errorMessage.set('Kimlik, gelir ve ikametgah belgelerinin dosyalarını seçmeden özete geçemezsiniz.');
      return;
    }
    const delivery = this.applicationForm.getRawValue();
    if (delivery.deliveryMethod === 'DifferentAddress') {
      const addressParts = [delivery.deliveryNeighborhood, delivery.deliveryStreet, delivery.deliveryAvenue,
        delivery.deliveryBuildingNo ? `Bina No: ${delivery.deliveryBuildingNo}` : '',
        delivery.deliveryApartmentNo ? `Daire: ${delivery.deliveryApartmentNo}` : '',
        delivery.deliveryFloor ? `Kat: ${delivery.deliveryFloor}` : '', delivery.deliveryPostalCode,
        `${delivery.deliveryDistrict} / ${delivery.deliveryCity}`].filter(Boolean);
      this.applicationForm.controls.deliveryAddress.setValue(addressParts.join(', '), { emitEvent: false });
    }
    if (delivery.deliveryMethod === 'RegisteredAddress' && delivery.deliveryAddress.trim().length < 10) {
      this.errorMessage.set('Müşterinin kayıtlı teslimat adresi eksik. Müşteri detayından adresi güncelleyin.');
      return;
    }
    if (delivery.deliveryMethod === 'DifferentAddress'
      && (!delivery.deliveryCity || !delivery.deliveryDistrict || !delivery.deliveryNeighborhood
        || !delivery.deliveryStreet || !delivery.deliveryBuildingNo || !delivery.deliveryPostalCode
        || !delivery.deliveryRecipientName
        || !/^\d{10,11}$/.test(delivery.deliveryPhone.replace(/\D/g, '')))) {
      this.errorMessage.set('Farklı teslimat adresi için il, ilçe, mahalle, sokak, bina no, posta kodu, alıcı ve telefon zorunludur.');
      return;
    }
    if (delivery.deliveryMethod === 'DifferentAddress' && delivery.saveDeliveryAddress
      && delivery.addressName.trim().length < 2) {
      this.errorMessage.set('Adresi kaydetmek için “Ev Adresi” veya “İş Adresi” gibi bir adres adı girin.');
      this.applicationForm.controls.addressName.markAsTouched();
      return;
    }
    if (delivery.deliveryMethod === 'Branch'
      && (!delivery.deliveryCity || !delivery.deliveryDistrict || !delivery.deliveryBranch)) {
      this.errorMessage.set('Şubeden teslim için il, ilçe ve teslimat şubesini seçin.');
      return;
    }
    const requestedLimit = this.applicationForm.controls.requestedLimit.value;
    if (this.duplicateWarning()?.hasDuplicate) {
      this.errorMessage.set('Aynı kart tipi için açık veya onaylı kayıt bulunduğundan yeni başvuru oluşturulamaz.');
      return;
    }
    const cardType = this.selectedCardType();
    if (!cardType) return;
    if (requestedLimit > this.availableLimit()) this.notice.set('Talep edilen limit hesaplanan azami limitin üzerindedir; nihai limit müdür değerlendirmesinde belirlenecektir.');
    this.step.set(3);
  }

  protected submit(): void {
    if (this.isLoading()) return;
    const customer = this.selectedCustomer();
    if (!customer) {
      this.showSubmissionError('Başvuru yapılacak müşteri seçilemedi. Müşteri seçimi adımına dönerek yeniden deneyin.');
      return;
    }
    if (this.applicationForm.invalid) {
      this.applicationForm.markAllAsTouched();
      this.showSubmissionError('Başvuru bilgileri eksik veya geçersiz. Bilgileri Düzenle seçeneğiyle işaretlenen alanları kontrol edin.');
      return;
    }
    if (!['Identity', 'Income', 'Residence'].every(type => this.selectedDocuments()[type])) {
      this.showSubmissionError('Kimlik, gelir ve ikametgah belgeleri seçilmeden başvuru tamamlanamaz.');
      return;
    }
    this.isLoading.set(true);
    this.errorMessage.set('');
    const rawValues = this.applicationForm.getRawValue();
    const { confirmed: _confirmed, deliveryPhoneCountryCode, deliveryStreet: _street,
      deliveryAvenue: _avenue, deliveryBuildingNo: _building, deliveryApartmentNo: _apartment,
      deliveryFloor: _floor, deliveryPostalCode: _postalCode, selectedSavedAddressId: _savedAddressId,
      addressName: _addressName, saveDeliveryAddress: _saveDeliveryAddress, ...values } = rawValues;
    this.applicationApiService.create({
      customerId: customer.id,
      ...values,
      deliveryCity: values.deliveryCity || null,
      deliveryDistrict: values.deliveryDistrict || null,
      deliveryNeighborhood: values.deliveryNeighborhood || null,
      deliveryRecipientName: values.deliveryRecipientName || null,
      deliveryPhone: values.deliveryPhone
        ? `${deliveryPhoneCountryCode}${values.deliveryPhone.replace(/\D/g, '')}`
        : null,
      deliveryBranch: values.deliveryBranch || null,
      applicationNote: values.applicationNote.trim() || null,
    }).subscribe({
      next: (application) => {
        const uploads = Object.entries(this.selectedDocuments())
          .map(([type, file]) => this.applicationApiService.uploadDocument(application.id, type, file));
        const addressSave = rawValues.deliveryMethod === 'DifferentAddress' && rawValues.saveDeliveryAddress
          ? this.customerApiService.addAddress(customer.id, {
              name: rawValues.addressName.trim(), city: rawValues.deliveryCity,
              district: rawValues.deliveryDistrict, neighborhood: rawValues.deliveryNeighborhood,
              street: rawValues.deliveryStreet, avenue: rawValues.deliveryAvenue.trim() || null,
              buildingNo: rawValues.deliveryBuildingNo, apartmentNo: rawValues.deliveryApartmentNo.trim() || null,
              floor: rawValues.deliveryFloor.trim() || null, postalCode: rawValues.deliveryPostalCode,
              isDefault: false,
            })
          : of(null);
        forkJoin([...uploads, addressSave]).subscribe({
          next: () => {
            this.clearDraft();
            this.createdApplication.set(application);
            this.isLoading.set(false);
            this.step.set(4);
            window.scrollTo({ top: 0, behavior: 'smooth' });
          },
          error: (error: HttpErrorResponse) => {
            this.createdApplication.set(application);
            this.isLoading.set(false);
            this.step.set(4);
            this.showSubmissionError(error.error?.detail ?? 'Başvuru oluşturuldu ancak belge veya kayıtlı adres işlemlerinden biri tamamlanamadı. Başvuru detayından kontrol edin.');
          },
        });
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);
        this.showSubmissionError(error.error?.detail ?? error.error?.title ?? 'Başvuru oluşturulamadı. Lütfen bilgileri kontrol edip yeniden deneyin.');
      },
    });
  }

  private showSubmissionError(message: string): void {
    this.errorMessage.set(message);
    queueMicrotask(() => {
      const alert = document.querySelector<HTMLElement>('.application-page [role="alert"]');
      alert?.focus();
      alert?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  protected selectDocument(type: string, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (!['application/pdf', 'image/jpeg', 'image/png'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      this.errorMessage.set('Belgeler PDF, JPG veya PNG biçiminde ve en fazla 5 MB olabilir.');
      (event.target as HTMLInputElement).value = '';
      return;
    }
    this.selectedDocuments.update(documents => ({ ...documents, [type]: file }));
  }

  protected documentName(type: string): string { return this.selectedDocuments()[type]?.name ?? 'Dosya seçilmedi'; }

  protected downloadCreatedPdf(): void {
    const application = this.createdApplication();
    if (!application) return;
    this.platformApi.applicationPdf(application.id).subscribe(blob => {
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a');
      anchor.href = url; anchor.download = `${application.applicationNumber}.pdf`; anchor.click(); URL.revokeObjectURL(url);
    });
  }

  protected printApplication(): void {
    const application = this.createdApplication();
    if (!application) return;
    this.platformApi.applicationPdf(application.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const frame = document.createElement('iframe');
        frame.hidden = true;
        frame.src = url;
        frame.onload = () => {
          frame.contentWindow?.focus();
          frame.contentWindow?.print();
          setTimeout(() => { frame.remove(); URL.revokeObjectURL(url); }, 1500);
        };
        document.body.appendChild(frame);
      },
      error: response => this.errorMessage.set(response.error?.detail ?? 'Yazdırılabilir başvuru PDF’i oluşturulamadı.'),
    });
  }

  protected goBack(): void {
    this.errorMessage.set('');
    this.step.set(this.step() === 3 ? 2 : 1);
  }

  protected startNewApplication(): void {
    this.clearDraft();
    this.query.reset('');
    this.searchResults.set([]);
    this.selectedCustomer.set(null);
    this.savedAddresses.set([]);
    this.createdApplication.set(null);
    this.duplicateWarning.set(null);
    this.selectedDocuments.set({});
    this.errorMessage.set('');
    this.hasSearched.set(false);
    this.applicationForm.reset();
    this.step.set(1);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected saveDraft(): void {
    const customer = this.selectedCustomer();
    if (!customer) { this.errorMessage.set('Taslak kaydetmek için önce müşteriyi seçin.'); return; }
    const savedAt = new Date().toISOString();
    localStorage.setItem(this.draftKey(), JSON.stringify({
      customerId: customer.id, step: this.step() === 3 ? 3 : 2,
      values: this.applicationForm.getRawValue(), savedAt,
    }));
    this.draftInfo.set({ customerId: customer.id, savedAt });
    this.notice.set('Başvuru taslağı kaydedildi. Belge dosyaları güvenlik nedeniyle taslağa eklenmez.');
  }

  protected restoreDraft(): void {
    const raw = localStorage.getItem(this.draftKey());
    if (!raw) { this.draftInfo.set(null); return; }
    try {
      const draft = JSON.parse(raw) as { customerId: number; step: 2 | 3; values: Record<string, unknown> };
      this.isLoading.set(true);
      this.customerApiService.getById(draft.customerId).subscribe({
        next: customer => {
          this.chooseCustomer(customer);
          this.applicationForm.patchValue(draft.values);
          this.step.set(draft.step === 3 ? 3 : 2);
          this.selectedDocuments.set({});
          this.notice.set('Taslak geri yüklendi. Kimlik, gelir ve ikametgah belgelerini yeniden seçin.');
          this.isLoading.set(false);
        },
        error: () => { this.errorMessage.set('Taslağın müşterisi artık erişilebilir değil.'); this.isLoading.set(false); },
      });
    } catch { this.clearDraft(); this.errorMessage.set('Taslak verisi okunamadığı için temizlendi.'); }
  }

  protected discardDraft(): void { this.clearDraft(); this.notice.set('Kaydedilmiş taslak silindi.'); }
  protected draftTime(): string { const value = this.draftInfo()?.savedAt; return value ? new Date(value).toLocaleString('tr-TR') : ''; }

  private draftKey(): string {
    try {
      const session = JSON.parse(sessionStorage.getItem('credit-card-auth') ?? '{}') as { user?: { id?: number } };
      return `card-application-draft-${session.user?.id ?? 'anonymous'}`;
    } catch { return 'card-application-draft-anonymous'; }
  }
  private readDraftInfo(): { customerId: number; savedAt: string } | null {
    try {
      const raw = localStorage.getItem(this.draftKey()); if (!raw) return null;
      const data = JSON.parse(raw) as { customerId: number; savedAt: string };
      return { customerId: data.customerId, savedAt: data.savedAt };
    } catch { return null; }
  }
  private clearDraft(): void { localStorage.removeItem(this.draftKey()); this.draftInfo.set(null); }

  protected availableBranches(): string[] {
    const city = this.applicationForm.controls.deliveryCity.value;
    const district = this.applicationForm.controls.deliveryDistrict.value;
    if (!city || !district) return [];
    const normalize = (value: string) => value.toLocaleLowerCase('tr-TR');
    const matches = this.branchCatalog
      .filter(item => normalize(item.city) === normalize(city) && normalize(item.district) === normalize(district))
      .map(item => `${item.name} — ${item.district}/${item.city}`);
    return matches.length ? matches : [`${district} Şubesi — ${district}/${city}`];
  }

  protected paymentDueLabel(statementDay: number): string {
    const dueDay = statementDay + 10;
    return dueDay <= 28
      ? `Ayın ${dueDay}. günü`
      : `Takip eden ayın ${dueDay - 28}. günü`;
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 }).format(value);
  }

  private loadCustomer(id: number): void {
    this.isLoading.set(true);
    this.customerApiService.getById(id).subscribe({
      next: (customer) => { this.chooseCustomer(customer); this.isLoading.set(false); },
      error: () => { this.errorMessage.set('Başvuru yapılacak müşteri bulunamadı.'); this.isLoading.set(false); },
    });
  }

  private loadDistricts(cityName: string): void {
    this.districts.set([]);
    this.neighborhoods.set([]);
    this.streetOptions.set([]);
    this.postalCodeStatus.set('');
    this.applicationForm.controls.deliveryDistrict.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryNeighborhood.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryStreet.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryPostalCode.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryBranch.setValue('', { emitEvent: false });
    const province = this.provinces().find(item => item.name === cityName);
    if (!province) return;
    this.addressCatalogService.getDistricts(province.id).subscribe({
      next: items => this.districts.set(items),
      error: () => this.errorMessage.set('İlçe listesi yüklenemedi.'),
    });
  }

  private loadNeighborhoods(districtName: string): void {
    this.neighborhoods.set([]);
    this.streetOptions.set([]);
    this.postalCodeStatus.set('');
    this.applicationForm.controls.deliveryNeighborhood.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryStreet.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryPostalCode.setValue('', { emitEvent: false });
    const district = this.districts().find(item => item.name === districtName);
    if (!district) return;
    this.addressCatalogService.getNeighborhoods(district.id).subscribe({
      next: items => this.neighborhoods.set(items),
      error: () => this.errorMessage.set('Mahalle listesi yüklenemedi.'),
    });
  }

  private loadNeighborhoodAddressData(neighborhoodName: string): void {
    this.streetOptions.set([]);
    this.applicationForm.controls.deliveryStreet.setValue('', { emitEvent: false });
    this.applicationForm.controls.deliveryPostalCode.setValue('', { emitEvent: false });
    const neighborhood = this.neighborhoods().find(item => item.name === neighborhoodName);
    this.postalCodeStatus.set(neighborhood?.postalCodeStatus ?? '');
    this.applicationForm.controls.deliveryPostalCode.setValue(neighborhood?.postalCode ?? '', { emitEvent: false });
    if (!neighborhood) return;
    this.addressLoading.set(true);
    this.addressCatalogService.getStreets(neighborhood.id).subscribe({
      next: items => { this.streetOptions.set(items.map(item => item.name)); this.addressLoading.set(false); },
      error: () => { this.addressLoading.set(false); this.errorMessage.set('Ulusal cadde/sokak kataloğu yüklenemedi.'); },
    });
  }

  private selectProductNetworkCombination(): void {
    const cardType = this.cardTypes().find(item =>
      item.productName === this.selectedProductName() && item.network === this.selectedNetworkName());
    if (cardType) this.chooseCardType(cardType);
  }
}
