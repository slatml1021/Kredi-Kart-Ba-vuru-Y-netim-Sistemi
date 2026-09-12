import { HttpErrorResponse } from '@angular/common/http';
import { AfterViewInit, Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { personnelNumberPattern, personnelPasswordPattern } from '../../../core/validators/business-validators';

@Component({ selector: 'app-login', imports: [ReactiveFormsModule], templateUrl: './login.html', styleUrl: './login.scss' })
export class Login implements AfterViewInit {
  protected readonly loginError = signal('');
  protected readonly supportNotice = signal('');
  protected readonly isSubmitting = signal(false);
  protected readonly isReporting = signal(false);
  protected readonly loginForm = new FormGroup({
    registrationNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(personnelNumberPattern)] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(personnelPasswordPattern)] }),
  });

  constructor(private readonly authService: AuthService, private readonly router: Router) {}

  ngAfterViewInit(): void {
    queueMicrotask(() => this.loginForm.reset({ registrationNumber: '', password: '' }));
  }

  protected normalizeRegistrationNumber(): void {
    const normalized = this.loginForm.controls.registrationNumber.value
      .toLocaleUpperCase('tr-TR').replace(/[^A-Z0-9]/g, '').slice(0, 9);
    this.loginForm.controls.registrationNumber.setValue(normalized, { emitEvent: false });
  }

  protected submit(): void {
    this.loginError.set('');
    this.supportNotice.set('');
    if (this.loginForm.invalid) { this.loginForm.markAllAsTouched(); return; }
    this.isSubmitting.set(true);
    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: (user) => void this.router.navigate(
        [user.role === 'Officer' ? '/officer/dashboard' : '/manager/dashboard'],
        { replaceUrl: true },
      ),
      error: (response: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.loginError.set(response.status === 423
          ? 'Hesap geçici olarak kilitlendi. Lütfen bir süre sonra yeniden deneyin.'
          : 'Sicil numarası veya şifre hatalıdır.');
      },
    });
  }

  protected reportProblem(): void {
    const registrationNumber = this.loginForm.controls.registrationNumber.value.trim();
    if (!personnelNumberPattern.test(registrationNumber)) {
      this.loginForm.controls.registrationNumber.markAsTouched();
      this.supportNotice.set('Sorun bildirmek için önce geçerli sicil numaranızı girin.');
      return;
    }
    this.isReporting.set(true);
    this.authService.reportLoginIssue(registrationNumber).subscribe({
      next: () => {
        this.isReporting.set(false);
        this.supportNotice.set('Sorun kaydınız yöneticilere ve destek ekibine iletildi.');
      },
      error: () => {
        this.isReporting.set(false);
        this.supportNotice.set('Sorun kaydı gönderilemedi. Lütfen daha sonra tekrar deneyin.');
      },
    });
  }
}
