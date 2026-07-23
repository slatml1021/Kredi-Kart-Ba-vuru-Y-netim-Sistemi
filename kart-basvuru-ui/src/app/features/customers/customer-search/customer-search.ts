import { Component, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CustomerApiService } from '../customer-api.service';
import { Customer } from '../customer.models';

@Component({
  selector: 'app-customer-search',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './customer-search.html',
  styleUrl: './customer-search.scss',
})
export class CustomerSearch {
  protected readonly query = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  protected readonly customers = signal<Customer[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly errorMessage = signal('');

  constructor(private readonly customerApiService: CustomerApiService) {}

  protected search(): void {
    this.errorMessage.set('');
    if (this.query.invalid) {
      this.query.markAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.customerApiService.search(this.query.value.trim()).subscribe({
      next: (customers) => {
        this.customers.set(customers);
        this.hasSearched.set(true);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Müşteri sorgulanırken bir hata oluştu. API bağlantısını kontrol edin.');
        this.hasSearched.set(true);
        this.isLoading.set(false);
      },
    });
  }
}
