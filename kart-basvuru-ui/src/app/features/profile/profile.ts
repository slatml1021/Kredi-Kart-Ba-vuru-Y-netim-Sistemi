import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PlatformApiService, UserProfile } from '../../core/services/platform-api.service';

@Component({
  selector: 'app-profile',
  imports: [DatePipe, ReactiveFormsModule],
  template: `
    <section class="profile-page">
      <p>PERSONEL / GÜVENLİK / YETKİLER</p><h1>Profilim</h1>
      @if (message()) { <div class="message">{{ message() }}</div> }
      @if (profile(); as item) {
        <article class="hero"><div class="avatar">{{ item.fullName.slice(0,2).toUpperCase() }}</div><div><h2>{{ item.fullName }}</h2><span>{{ item.title }}</span><small>Sicil No: {{ item.registrationNumber }}</small></div><b [class.inactive]="!item.isActive">{{ item.isActive ? 'Aktif Hesap' : 'Pasif Hesap' }}</b></article>
        <div class="summary">
          <div><span>Kurumsal E-posta</span><b>{{ item.corporateEmail }}</b></div><div><span>Rol / Ünvan</span><b>{{ item.role === 'Manager' ? 'Müdür' : 'Memur' }} · {{ item.title }}</b></div><div><span>Birim</span><b>{{ item.department }}</b></div><div><span>Lokasyon</span><b>{{ item.branch }}</b></div><div><span>Sisteme Kayıt</span><b>{{ item.registeredAtUtc | date:'dd.MM.yyyy' }}</b></div>
        </div>
        @if (item.role === 'Manager') {
          <section><h2>Ekip Özeti</h2><div class="metrics"><div><b>{{ item.activeOfficerCount }}</b><span>Aktif Memur</span></div><div><b>{{ item.applicationsToday }}</b><span>Bugünkü Başvuru</span></div><div><b>{{ item.revisionWaiting }}</b><span>Revizyon Bekleyen</span></div><div><b>{{ item.overdueApplications }}</b><span>Geciken Başvuru</span></div></div></section>
        }
        <section><h2>Güvenlik Bilgileri</h2><div class="summary"><div><span>Son Başarılı Giriş</span><b>{{ item.lastSuccessfulLoginUtc ? (item.lastSuccessfulLoginUtc | date:'dd.MM.yyyy HH:mm') : '—' }}</b></div><div><span>Son Başarısız Giriş</span><b>{{ item.lastFailedLoginUtc ? (item.lastFailedLoginUtc | date:'dd.MM.yyyy HH:mm') : '—' }}</b></div><div><span>Şifre Değişikliği</span><b>{{ item.passwordChangedAtUtc | date:'dd.MM.yyyy' }}</b></div><div><span>İki Aşamalı Doğrulama</span><b>{{ item.twoFactorEnabled ? 'Açık' : 'Kapalı' }}</b></div><div><span>Hesap Kilidi</span><b>{{ item.isLocked ? 'Kilitli' : 'Kilitli Değil' }}</b></div></div>
          <div class="table"><table><thead><tr><th>TARİH</th><th>SONUÇ</th><th>CİHAZ</th><th>IP</th></tr></thead><tbody>@for(log of item.loginHistory;track log.dateUtc){<tr><td>{{log.dateUtc|date:'dd.MM.yyyy HH:mm'}}</td><td [class.fail]="!log.isSuccessful">{{log.isSuccessful?'Başarılı':'Başarısız'}}</td><td>{{log.device}}</td><td>{{log.ipAddress||'—'}}</td></tr>}</tbody></table></div>
        </section>
        <div class="two-column">
          <section><h2>Son İşlemler</h2><ul class="activity">@for(action of item.recentActivities;track action.dateUtc){<li><span>{{action.dateUtc|date:'dd.MM.yyyy HH:mm'}}</span><b>{{action.description}}</b></li>}@empty{<li>Henüz işlem kaydı yok.</li>}</ul></section>
          <section><h2>Yetkilerim</h2><ul class="permissions">@for(permission of item.grantedPermissions;track permission){<li class="yes">✓ {{permission}}</li>}@for(permission of item.deniedPermissions;track permission){<li class="no">✕ {{permission}}</li>}</ul><small>Yetkiler profil üzerinden değiştirilemez; sistem yöneticisi tarafından yönetilir.</small></section>
        </div>
        <section><h2>Düzenlenebilir Bilgiler ve Bildirim Tercihleri</h2><form [formGroup]="form" (ngSubmit)="save()"><label>Telefon Numarası<input formControlName="phoneNumber"></label><label>Profil Fotoğrafı URL<input formControlName="profilePhotoUrl"></label><label class="check"><input type="checkbox" formControlName="notifyApplicationEvents"> Başvuru olayları</label><label class="check"><input type="checkbox" formControlName="notifySlaWarnings"> SLA / bekleme uyarıları</label><label class="check"><input type="checkbox" formControlName="notifySecurityEvents"> Güvenlik ve başarısız giriş uyarıları</label><button>Tercihleri Kaydet</button></form><small>Sicil, rol, ünvan, birim, hesap durumu ve işe giriş bilgisi yalnızca sistem yöneticisi tarafından değiştirilebilir.</small></section>
      } @else { <p>Profil bilgileri yükleniyor...</p> }
    </section>
  `,
  styles: `
    :host{display:block}.profile-page{max-width:1200px;margin:auto;padding:32px;color:#20364d}.profile-page>p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.16em}h1{margin:7px 0 22px;color:#0b2a4d;font-size:32px}.message{padding:12px;margin-bottom:13px;border-radius:7px;background:#edf6ff}.hero{display:flex;align-items:center;gap:16px;padding:22px;border-radius:12px;color:#fff;background:linear-gradient(135deg,#092847,#174d78)}.hero .avatar{width:68px;height:68px;display:grid;place-items:center;border-radius:50%;background:#d2a83d;font-size:20px;font-weight:900}.hero h2{margin:0}.hero span,.hero small{display:block;margin-top:4px;color:#c7d6e4}.hero>b{margin-left:auto;padding:7px 10px;border-radius:20px;background:#21825a}.hero>b.inactive{background:#aa3c3c}.summary{display:grid;grid-template-columns:repeat(5,1fr);gap:10px;margin-top:14px}.summary>div,.metrics>div{padding:14px;border:1px solid #e0e6eb;border-radius:8px;background:#f8fafb;display:grid;gap:5px}.summary span,.metrics span{color:#7b8998;font-size:9px}.summary b{font-size:11px}section{margin-top:16px;padding:20px;border:1px solid #dce3e9;border-radius:10px;background:#fff}section h2{margin:0 0 14px;color:#18344e;font-size:16px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}.metrics b{font-size:25px;color:#0b315b}.two-column{display:grid;grid-template-columns:1fr 1fr;gap:16px}.activity,.permissions{margin:0;padding:0;list-style:none}.activity li,.permissions li{padding:10px;border-bottom:1px solid #edf0f3}.activity span{display:block;color:#8492a0;font-size:9px}.permissions .yes{color:#18734d}.permissions .no{color:#a53b3b}.table{margin-top:14px;overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:10px;border-bottom:1px solid #edf0f3;text-align:left;font-size:11px}th{color:#758595;font-size:8px}.fail{color:#b03c3c}form{display:grid;grid-template-columns:1fr 1fr;gap:12px}label{display:grid;gap:6px;font-weight:700}input{padding:10px;border:1px solid #cad5df;border-radius:7px}.check{display:flex;align-items:center;gap:8px;font-weight:500}.check input{width:auto}button{grid-column:1/-1;padding:11px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}section>small{display:block;margin-top:12px;color:#778797}@media(max-width:850px){.summary{grid-template-columns:1fr 1fr}.two-column{grid-template-columns:1fr}.metrics{grid-template-columns:1fr 1fr}}@media(max-width:540px){.profile-page{padding:20px}.summary,form{grid-template-columns:1fr}.hero{align-items:flex-start;flex-direction:column}.hero>b{margin-left:0}}
  `,
})
export class Profile implements OnInit {
  protected readonly profile = signal<UserProfile | null>(null);
  protected readonly message = signal('');
  protected readonly form = new FormGroup({
    phoneNumber: new FormControl('', { nonNullable: true }),
    profilePhotoUrl: new FormControl('', { nonNullable: true }),
    notifyApplicationEvents: new FormControl(true, { nonNullable: true }),
    notifySlaWarnings: new FormControl(true, { nonNullable: true }),
    notifySecurityEvents: new FormControl(true, { nonNullable: true }),
  });
  constructor(protected readonly auth: AuthService, private readonly api: PlatformApiService) {}
  ngOnInit() { this.load(); }
  protected save() {
    const value = this.form.getRawValue();
    this.api.updateProfile({ ...value, profilePhotoUrl: value.profilePhotoUrl || null }).subscribe({
      next: profile => { this.profile.set(profile); this.message.set('Profil ve bildirim tercihleri kaydedildi.'); },
      error: response => this.message.set(response.error?.detail ?? 'Profil kaydedilemedi.'),
    });
  }
  private load() {
    this.api.profile().subscribe(profile => {
      this.profile.set(profile);
      this.form.patchValue({
        phoneNumber: profile.phoneNumber, profilePhotoUrl: '',
        notifyApplicationEvents: profile.notifyApplicationEvents,
        notifySlaWarnings: profile.notifySlaWarnings,
        notifySecurityEvents: profile.notifySecurityEvents,
      });
    });
  }
}
