import { Component, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerApiService } from '../customer-api.service';
import { Customer } from '../customer.models';
import { digitsOnly, isValidTurkishIdentityNumber } from '../../../core/validators/business-validators';

@Component({
  selector: 'app-customer-search',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './customer-search.html',
  styleUrl: './customer-search.scss',
})
export class CustomerSearch {
  protected readonly criterion = new FormControl<'NationalIdentityNumber' | 'CustomerNumber' | 'PhoneNumber'>(
    'NationalIdentityNumber', { nonNullable: true },
  );
  protected readonly query = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  protected readonly phoneCountryCode = new FormControl('+90', { nonNullable: true });
  protected readonly phoneCountries = [
    { code: '+90', label: 'Türkiye (+90)', pattern: /^5\d{9}$/, placeholder: '5XXXXXXXXX' },
    { code: '+44', label: 'İngiltere (+44)', pattern: /^7\d{9}$/, placeholder: '7XXXXXXXXX' },
    { code: '+1', label: 'ABD (+1)', pattern: /^[2-9]\d{9}$/, placeholder: 'XXXXXXXXXX' },
    { code: '+49', label: 'Almanya (+49)', pattern: /^\d{10,11}$/, placeholder: 'XXXXXXXXXX' },
  ];
  protected readonly customers = signal<Customer[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly errorMessage = signal('');

  constructor(
    private readonly customerApiService: CustomerApiService,
    private readonly router: Router,
  ) {}

  protected search(): void {
    this.errorMessage.set('');
    if (this.query.invalid) {
      this.query.markAsTouched();
      return;
    }

    this.isLoading.set(true);
    const value = this.query.value.trim();
    const isNationalIdentitySearch = this.criterion.value === 'NationalIdentityNumber';
    const isPhoneSearch = this.criterion.value === 'PhoneNumber';
    const normalizedPhone = value.replace(/\D/g, '');
    const phoneCountry = this.phoneCountries.find(item => item.code === this.phoneCountryCode.value)!;
    const isValidPhone = phoneCountry.pattern.test(normalizedPhone);
    const isCustomerNumberSearch = this.criterion.value === 'CustomerNumber';
    if ((isNationalIdentitySearch && !isValidTurkishIdentityNumber(value))
      || (isCustomerNumberSearch && !/^MUS\d{6}$/.test(value.toUpperCase()))
      || (isPhoneSearch && !isValidPhone)) {
      this.errorMessage.set(isPhoneSearch
        ? 'Telefonu ülke koduyla veya seçilen ülkenin yerel mobil formatıyla girin.'
        : isCustomerNumberSearch
          ? 'Müşteri numarası MUS ile başlamalı ve ardından 6 rakam gelmelidir.'
          : 'Geçerli bir TC Kimlik numarası girilmelidir.');
      this.hasSearched.set(true);
      this.isLoading.set(false);
      return;
    }

    const searchValue = isPhoneSearch ? `${this.phoneCountryCode.value.replace('+', '')}${normalizedPhone}` : value;
    this.customerApiService.search(searchValue, this.criterion.value).subscribe({
      next: (customers) => {
        this.customers.set(customers);
        this.hasSearched.set(true);
        this.isLoading.set(false);
        if (customers.length === 1) {
          void this.router.navigate(['/officer/customers', customers[0].id]);
        }
      },
      error: () => {
        this.errorMessage.set('Müşteri sorgulanırken bir hata oluştu. API bağlantısını kontrol edin.');
        this.hasSearched.set(true);
        this.isLoading.set(false);
      },
    });
  }

  protected clear(): void {
    this.query.reset('');
    this.customers.set([]);
    this.hasSearched.set(false);
    this.errorMessage.set('');
  }

  protected normalizeQuery(): void {
    const current = this.query.value;
    if (this.criterion.value === 'NationalIdentityNumber') {
      this.query.setValue(digitsOnly(current, 11), { emitEvent: false });
    } else if (this.criterion.value === 'CustomerNumber') {
      this.query.setValue(current.toLocaleUpperCase('tr-TR').replace(/[^A-Z0-9]/g, '').slice(0, 9), { emitEvent: false });
    } else {
      const maximum = this.phoneCountryCode.value === '+49' ? 11 : 10;
      this.query.setValue(current.replace(/\D/g, '').slice(0, maximum), { emitEvent: false });
    }
  }

  protected placeholder(): string {
    return this.criterion.value === 'NationalIdentityNumber'
      ? '11 haneli TC kimlik numarasını girin'
      : this.criterion.value === 'CustomerNumber'
        ? 'MUS000001'
        : this.phoneCountries.find(item => item.code === this.phoneCountryCode.value)?.placeholder ?? 'Telefon numarası';
  }
}
