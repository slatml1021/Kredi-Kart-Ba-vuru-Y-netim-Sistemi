import { Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({ selector: 'app-login', imports: [ReactiveFormsModule], templateUrl: './login.html', styleUrl: './login.scss' })
export class Login {
  protected readonly showPassword = signal(false);
  protected readonly loginError = signal('');
  protected readonly loginForm = new FormGroup({
    registrationNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(4)] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
  });

  constructor(private readonly authService: AuthService, private readonly router: Router) {}

  protected togglePasswordVisibility(): void { this.showPassword.update((value) => !value); }

  protected submit(): void {
    this.loginError.set('');
    if (this.loginForm.invalid) { this.loginForm.markAllAsTouched(); return; }
    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: (user) => void this.router.navigate([user.role === 'Officer' ? '/officer/dashboard' : '/manager/dashboard']),
      error: () => this.loginError.set('Sicil numarası veya şifre hatalıdır.'),
    });
  }
}
