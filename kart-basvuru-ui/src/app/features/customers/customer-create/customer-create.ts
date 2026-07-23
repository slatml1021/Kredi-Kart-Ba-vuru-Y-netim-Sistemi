import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerApiService } from '../customer-api.service';
import { CreateCustomerRequest } from '../customer.models';

@Component({
  selector: 'app-customer-create',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './customer-create.html',
  styleUrl: './customer-create.scss',
})
export class CustomerCreate {
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form;

  constructor(
    formBuilder: FormBuilder,
    private readonly customerApiService: CustomerApiService,
    private readonly router: Router,
  ) {
    this.form = formBuilder.nonNullable.group({
      nationalIdentityNumber: ['', [Validators.required, Validators.pattern(/^\d{11}$/)]],
      firstName: ['', [Validators.required, Validators.maxLength(60)]],
      lastName: ['', [Validators.required, Validators.maxLength(60)]],
      phoneNumber: ['', [Validators.required, Validators.pattern(/^\d{11}$/)]],
      emailAddress: ['', [Validators.required, Validators.email]],
      monthlyNetIncome: [0, [Validators.required, Validators.min(1)]],
      otherBankTotalCardLimit: [0, [Validators.required, Validators.min(0)]],
    });
  }

  protected save(): void {
    this.errorMessage.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const request: CreateCustomerRequest = this.form.getRawValue();
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
}
