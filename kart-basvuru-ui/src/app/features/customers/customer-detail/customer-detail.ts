import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CustomerApiService } from '../customer-api.service';
import { Customer } from '../customer.models';

@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.scss',
})
export class CustomerDetail implements OnInit {
  protected readonly customer = signal<Customer | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly wasCreated = history.state.customerCreated === true;
  protected readonly isEditing = signal(false);
  protected readonly updateMessage = signal('');
  protected readonly editForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\d{11}$/)] }),
    emailAddress: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
    monthlyNetIncome: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
    otherBankTotalCardLimit: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly customerApiService: CustomerApiService,
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(id) || id <= 0) {
      this.errorMessage.set('Geçersiz müşteri numarası.');
      this.isLoading.set(false);
      return;
    }

    this.customerApiService.getById(id).subscribe({
      next: (customer) => { this.customer.set(customer); this.editForm.setValue({ firstName: customer.firstName, lastName: customer.lastName, phoneNumber: customer.phoneNumber, emailAddress: customer.emailAddress, monthlyNetIncome: customer.monthlyNetIncome, otherBankTotalCardLimit: customer.otherBankTotalCardLimit }); this.isLoading.set(false); },
      error: () => { this.errorMessage.set('Müşteri bulunamadı veya API bağlantısı kurulamadı.'); this.isLoading.set(false); },
    });
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 }).format(value);
  }

  protected save(): void {
    const customer = this.customer();
    if (!customer || this.editForm.invalid) { this.editForm.markAllAsTouched(); return; }
    this.customerApiService.update(customer.id, this.editForm.getRawValue()).subscribe({
      next: (updated) => { this.customer.set(updated); this.isEditing.set(false); this.updateMessage.set('Müşteri bilgileri güncellendi.'); },
      error: (response) => this.errorMessage.set(response.error?.detail ?? 'Müşteri bilgileri güncellenemedi.'),
    });
  }
}
