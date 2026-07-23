import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerApiService } from '../../customers/customer-api.service';
import { Customer } from '../../customers/customer.models';
import { CardApplicationApiService } from '../card-application-api.service';
import { CardApplication, CardType } from '../card-application.models';

@Component({
  selector: 'app-application-create',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './application-create.html',
  styleUrl: './application-create.scss',
})
export class ApplicationCreate implements OnInit {
  protected readonly step = signal<1 | 2 | 3 | 4>(1);
  protected readonly query = new FormControl('', { nonNullable: true, validators: Validators.required });
  protected readonly searchResults = signal<Customer[]>([]);
  protected readonly selectedCustomer = signal<Customer | null>(null);
  protected readonly cardTypes = signal<CardType[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly createdApplication = signal<CardApplication | null>(null);
  protected readonly applicationForm;
  protected readonly availableLimit = computed(() => {
    const customer = this.selectedCustomer();
    return customer ? Math.max(0, customer.monthlyNetIncome * 3 - customer.otherBankTotalCardLimit) : 0;
  });
  protected readonly selectedCardType = computed(() =>
    this.cardTypes().find((item) => item.id === this.applicationForm.controls.cardTypeId.value) ?? null,
  );

  constructor(
    formBuilder: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly customerApiService: CustomerApiService,
    private readonly applicationApiService: CardApplicationApiService,
  ) {
    this.applicationForm = formBuilder.nonNullable.group({
      cardTypeId: [0, [Validators.required, Validators.min(1)]],
      requestedLimit: [0, [Validators.required, Validators.min(1)]],
    });
  }

  ngOnInit(): void {
    this.applicationApiService.getCardTypes().subscribe({
      next: (types) => this.cardTypes.set(types),
      error: () => this.errorMessage.set('Kart tipleri yüklenemedi. API bağlantısını kontrol edin.'),
    });

    const customerId = Number(this.route.snapshot.queryParamMap.get('customerId'));
    if (Number.isInteger(customerId) && customerId > 0) {
      this.loadCustomer(customerId);
    }
  }

  protected searchCustomer(): void {
    this.errorMessage.set('');
    if (this.query.invalid) {
      this.query.markAsTouched();
      return;
    }
    this.isLoading.set(true);
    this.customerApiService.search(this.query.value.trim()).subscribe({
      next: (customers) => { this.searchResults.set(customers); this.isLoading.set(false); },
      error: () => { this.errorMessage.set('Müşteri sorgulanamadı. API bağlantısını kontrol edin.'); this.isLoading.set(false); },
    });
  }

  protected chooseCustomer(customer: Customer): void {
    this.selectedCustomer.set(customer);
    this.searchResults.set([]);
    this.errorMessage.set('');
    this.step.set(2);
  }

  protected chooseCardType(cardType: CardType): void {
    this.applicationForm.controls.cardTypeId.setValue(cardType.id);
    const suggestedLimit = Math.min(this.availableLimit(), cardType.maximumLimit ?? this.availableLimit());
    this.applicationForm.controls.requestedLimit.setValue(suggestedLimit);
  }

  protected goToSummary(): void {
    this.errorMessage.set('');
    if (this.applicationForm.invalid) {
      this.applicationForm.markAllAsTouched();
      return;
    }
    const requestedLimit = this.applicationForm.controls.requestedLimit.value;
    const cardType = this.selectedCardType();
    if (!cardType) return;
    if (requestedLimit > this.availableLimit()) {
      this.errorMessage.set('Talep edilen limit müşterinin kullanılabilir limitini aşamaz.');
      return;
    }
    if (cardType.minimumLimit !== null && requestedLimit < cardType.minimumLimit) {
      this.errorMessage.set(`${cardType.name} kart için minimum limit ${this.formatCurrency(cardType.minimumLimit)} olmalıdır.`);
      return;
    }
    if (cardType.maximumLimit !== null && requestedLimit > cardType.maximumLimit) {
      this.errorMessage.set(`${cardType.name} kart için maksimum limit ${this.formatCurrency(cardType.maximumLimit)} olmalıdır.`);
      return;
    }
    this.step.set(3);
  }

  protected submit(): void {
    const customer = this.selectedCustomer();
    if (!customer || this.applicationForm.invalid) return;
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.applicationApiService.create({
      customerId: customer.id,
      ...this.applicationForm.getRawValue(),
    }).subscribe({
      next: (application) => { this.createdApplication.set(application); this.isLoading.set(false); this.step.set(4); },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.error?.detail ?? error.error?.title ?? 'Başvuru oluşturulamadı.');
        this.isLoading.set(false);
      },
    });
  }

  protected goBack(): void {
    this.errorMessage.set('');
    this.step.set(this.step() === 3 ? 2 : 1);
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
}
