import { Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerPortalService } from '../../core/services/customer-portal.service';

@Component({
  selector:'app-customer-login',imports:[ReactiveFormsModule,RouterLink],
  template:`<main><section><div class="brand">KART BAŞVURU <span>MÜŞTERİ PORTALI</span></div><h1>Hoş Geldiniz</h1><p>Başvurularınızı, kartlarınızı ve simülasyon sonucunu güvenli müşteri ekranından takip edin.</p>@if(error()){<div class="error" role="alert"><span>{{error()}}</span><button type="button" aria-label="Uyarıyı kapat" (click)="error.set('')">×</button></div>}<form [formGroup]="form" (ngSubmit)="login()"><label>Müşteri Numarası<input formControlName="customerNumber" autocomplete="off" placeholder="Müşteri numaranız"></label><label>Şifre<input type="password" formControlName="password" autocomplete="new-password"></label><button>Müşteri Girişi</button></form><a routerLink="/login">← Personel girişine dön</a></section></main>`,
  styles:`:host{display:block}main{min-height:100vh;display:grid;place-items:center;padding:20px;background:linear-gradient(135deg,#071a31,#174d78)}section{width:min(430px,100%);padding:34px;border-radius:16px;background:#fff;box-shadow:0 30px 80px #03101f66}.brand{color:#0b2a4d;font-weight:900;letter-spacing:.12em}.brand span{display:block;margin-top:3px;color:#b18722;font-size:10px}h1{margin:26px 0 6px;color:#0b2a4d}p,small{color:#718093;line-height:1.5}form{display:grid;gap:14px;margin:22px 0}label{display:grid;gap:7px;color:#243a50;font-weight:800}input{padding:12px;border:1px solid #cbd5df;border-radius:8px}button{padding:12px;border:0;border-radius:8px;color:#fff;background:#0b2a4d;font-weight:900}.error{display:flex;align-items:flex-start;justify-content:space-between;gap:10px;margin-top:12px;padding:10px;background:#fff0f0;color:#a53a3a}.error button{padding:0;color:inherit;background:transparent;font-size:18px}a{display:block;margin-top:20px;color:#174d78;text-decoration:none;font-weight:800}`,
})
export class CustomerLogin {
  protected readonly error=signal('');
  protected readonly form=new FormGroup({customerNumber:new FormControl('',{nonNullable:true,validators:[Validators.required]}),password:new FormControl('',{nonNullable:true,validators:[Validators.required]})});
  constructor(private readonly service:CustomerPortalService,private readonly router:Router){}
  protected login(){if(this.form.invalid)return;this.service.login(this.form.controls.customerNumber.value,this.form.controls.password.value).subscribe({next:()=>this.router.navigate(['/customer/dashboard']),error:e=>this.error.set(e.error?.detail??'Giriş yapılamadı.')});}
}
