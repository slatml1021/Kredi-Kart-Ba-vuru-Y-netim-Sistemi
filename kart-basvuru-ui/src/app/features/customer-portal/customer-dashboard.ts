import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CustomerPortalDashboard,
  CustomerPortalService,
} from '../../core/services/customer-portal.service';

@Component({
  selector: 'app-customer-dashboard',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  template: `
    <div class="shell">
      <header><b>Kart Başvuru <span>Müşteri Portalı</span></b><div>{{ service.session()?.fullName }} <button (click)="logout()">Çıkış</button></div></header>
      <main>
        @if (message()) { <div class="message">{{ message() }}<button type="button" (click)="message.set('')">×</button></div> }
        @if (data(); as item) {
          <h1>Merhaba, {{ item.customer.fullName }}</h1>
          <p>Başvurularınızı ve kart teslimat süreçlerinizi güvenli biçimde takip edin.</p>
          <div class="grid">
            <section>
              <h2>Başvurularım</h2>
              @for (application of item.applications; track application.id) {
                <article><b>{{ application.applicationNumber }}</b><span>{{ application.cardType }} · {{ application.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</span><small>{{ status(application.status) }} · {{ application.createdAtUtc | date:'dd.MM.yyyy' }}</small></article>
              } @empty { <p>Başvuru yok.</p> }
            </section>
            <section>
              <h2>Ana Kartlarım</h2>
              @for (card of item.cards; track card.id) {
                <article>
                  <b>{{ card.maskedCardNumber }}</b>
                  <span>{{ card.cardType }} · {{ card.cardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</span>
                  <small>{{ cardStatus(card.status) }} · {{ fulfillment(card.fulfillmentStatus) }}</small>
                  @if (card.estimatedDeliveryAtUtc && card.fulfillmentStatus !== 'Delivered') { <small>Tahmini teslim: {{ card.estimatedDeliveryAtUtc | date:'dd.MM.yyyy HH:mm' }}</small> }
                  @if (card.status === 'Inactive' && card.fulfillmentStatus === 'Delivered') { <button class="activate" type="button" (click)="requestActivation(card.id, false)">Kartı Aktive Et</button> }
                </article>
              } @empty { <p>Kart yok.</p> }
            </section>
          </div>
          <section class="supplementary">
            <h2>Adıma Düzenlenen Ek Kartlar</h2>
            <div class="card-grid">
              @for (card of item.supplementaryCards; track card.id) {
                <article>
                  <b>{{ card.maskedCardNumber || 'Kart numarası henüz oluşturulmadı' }}</b>
                  <span>Ana kart sahibi: {{ card.primaryCardHolder }} · {{ card.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</span>
                  <small>{{ cardStatus(card.cardStatus) }} · {{ fulfillment(card.fulfillmentStatus) }}</small>
                  @if (card.estimatedDeliveryAtUtc && card.fulfillmentStatus !== 'Delivered') { <small>Tahmini teslim: {{ card.estimatedDeliveryAtUtc | date:'dd.MM.yyyy HH:mm' }}</small> }
                  @if (card.cardStatus === 'Inactive' && card.fulfillmentStatus === 'Delivered') { <button class="activate" type="button" (click)="requestActivation(card.id, true)">Ek Kartı Aktive Et</button> }
                </article>
              } @empty { <p>Adınıza düzenlenmiş ek kart yok.</p> }
            </div>
          </section>
          <section class="simulation">
            <h2>Başvuru Simülasyonu</h2>
            <form [formGroup]="form" (ngSubmit)="simulate()">
              <select formControlName="cardTypeId"><option [value]="1">Classic Visa</option><option [value]="2">Gold World</option><option [value]="3">Platinum Visa</option><option [value]="4">Platinum Plus Visa</option><option [value]="5">Classic Troy</option><option [value]="6">Gold Troy</option></select>
              <input type="number" formControlName="requestedLimit" placeholder="Talep edilen limit">
              <button>Simüle Et</button>
            </form>
            @if (simulation(); as simulationResult) { <div class="result"><b>{{ simulationResult.canCreateApplication ? 'Başvuru oluşturulabilir' : 'Düzeltme gerekli' }}</b><span>Öneri: {{ simulationResult.recommendedCard }} · {{ simulationResult.suggestedMinimumLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}–{{ simulationResult.suggestedMaximumLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</span><small>Ön değerlendirme: {{ simulationResult.preAssessment.score }}/100 · {{ simulationResult.preAssessment.riskLevel }}</small><p>{{ simulationResult.disclaimer }}</p></div> }
          </section>
          @if (activationTarget(); as target) {
            <div class="overlay" (click)="activationTarget.set(null)"><form class="activation-dialog" (ngSubmit)="confirmActivation()" (click)="$event.stopPropagation()">
              <div><h2>Kart Aktivasyonu</h2><button type="button" (click)="activationTarget.set(null)">×</button></div>
              <p>6 haneli doğrulama kodu {{ target.destination }} adresine gönderildi. Kart yalnızca teslimat tamamlandıktan sonra aktive edilir.</p>
              @if (target.demoCode) { <small>Demo ortamı kodu: <b>{{ target.demoCode }}</b></small> }
              <input [formControl]="activationCode" inputmode="numeric" maxlength="6" placeholder="6 haneli aktivasyon kodu">
              <button type="submit" [disabled]="activationCode.invalid">Aktivasyonu Onayla</button>
            </form></div>
          }
        } @else { <p>Yükleniyor...</p> }
      </main>
    </div>
  `,
  styles: `
    :host{display:block;min-height:100vh;background:#f3f6f8;color:#20364d}header{height:72px;padding:0 max(24px,calc((100vw - 1200px)/2));display:flex;justify-content:space-between;align-items:center;color:#fff;background:#092847}header b span{display:block;color:#d5b75f;font-size:9px}header button{margin-left:12px;padding:8px 11px;border:1px solid #ffffff55;border-radius:6px;color:#fff;background:transparent}main{max-width:1200px;margin:auto;padding:32px}.message{display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;padding:13px 16px;border-radius:8px;color:#164c77;background:#eaf4fc}.message button{border:0;color:inherit;background:transparent;font-size:20px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}section{padding:20px;border:1px solid #dce3e9;border-radius:10px;background:#fff}section h2{margin-top:0}.supplementary,.simulation{margin-top:16px}.card-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}article{margin:8px 0;padding:13px;border-radius:8px;background:#f5f8fa;display:grid;gap:5px}article span,article small{color:#718093}.activate{justify-self:start;margin-top:5px;padding:8px 12px;border:0;border-radius:6px;color:#fff;background:#17734d;font-weight:800}form{display:grid;grid-template-columns:1fr 1fr auto;gap:9px}input,select{padding:10px;border:1px solid #cad5df;border-radius:7px}form button{border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}.result{margin-top:14px;padding:14px;border-left:4px solid #d1a83f;background:#fff9e9;display:grid;gap:6px}.result span,.result small{color:#5f7183}.overlay{position:fixed;inset:0;z-index:30;display:grid;place-items:center;padding:20px;background:#071a2db5}.activation-dialog{width:min(460px,100%);display:grid;grid-template-columns:1fr;gap:14px;padding:22px;border-radius:12px;background:#fff;box-shadow:0 25px 70px #0004}.activation-dialog>div{display:flex;justify-content:space-between;align-items:center}.activation-dialog h2,.activation-dialog p{margin:0}.activation-dialog>div button{padding:0;color:#20364d;background:transparent;font-size:24px}.activation-dialog small{padding:9px;border-radius:6px;background:#fff7dc}@media(max-width:750px){main{padding:20px}.grid,.card-grid{grid-template-columns:1fr}form{grid-template-columns:1fr}form button{padding:10px}}
  `,
})
export class CustomerDashboard implements OnInit {
  protected readonly data = signal<CustomerPortalDashboard | null>(null);
  protected readonly simulation = signal<any>(null);
  protected readonly message = signal('');
  protected readonly activationTarget = signal<{ id:number; supplementary:boolean; destination:string; demoCode:string|null } | null>(null);
  protected readonly activationCode = new FormControl('', { nonNullable:true, validators:[Validators.required, Validators.pattern(/^\d{6}$/)] });
  protected readonly form = new FormGroup({
    cardTypeId: new FormControl(1, { nonNullable: true }),
    requestedLimit: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
  });

  constructor(protected readonly service: CustomerPortalService, private readonly router: Router) {}

  ngOnInit(): void {
    if (!this.service.session()) { void this.router.navigate(['/customer/login']); return; }
    this.loadDashboard();
  }

  protected simulate(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.service.simulate({ cardTypeId: value.cardTypeId, requestedLimit: value.requestedLimit! })
      .subscribe(result => this.simulation.set(result));
  }

  protected requestActivation(id: number, supplementary: boolean): void {
    const request = supplementary ? this.service.requestSupplementaryActivationCode(id) : this.service.requestCardActivationCode(id);
    request.subscribe({
      next: response => { this.activationCode.reset(''); this.activationTarget.set({ id, supplementary, destination: response.maskedDestination, demoCode: response.demoCode }); },
      error: response => this.message.set(response.error?.detail ?? 'Aktivasyon kodu gönderilemedi.'),
    });
  }

  protected confirmActivation(): void {
    const target = this.activationTarget();
    if (!target || this.activationCode.invalid) return;
    const request = target.supplementary
      ? this.service.activateSupplementaryCard(target.id, this.activationCode.value)
      : this.service.activateCard(target.id, this.activationCode.value);
    request.subscribe({
      next: () => { this.message.set('Kartınız başarıyla aktive edildi.'); this.loadDashboard(); },
      error: response => this.message.set(response.error?.detail ?? 'Kart aktive edilemedi.'),
    });
    this.activationTarget.set(null);
  }

  protected status(value: string): string { return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda', Withdrawn: 'Geri Çekildi', Cancelled: 'İptal Edildi' } as Record<string, string>)[value] ?? value; }
  protected cardStatus(value: string): string { return ({ NotCreated: 'Henüz oluşturulmadı', Inactive: 'Pasif', Active: 'Aktif', Blocked: 'Blokeli', Expired: 'Süresi doldu', Cancelled: 'İptal edildi' } as Record<string, string>)[value] ?? value; }
  protected fulfillment(value: string | null): string { return ({ Production: 'Üretimde', Printing: 'Basımda', Shipped: 'Kuryede', Delivered: 'Teslim edildi' } as Record<string, string>)[value ?? ''] ?? 'Süreç başlamadı'; }
  protected logout(): void { this.service.logout(); void this.router.navigate(['/customer/login']); }

  private loadDashboard(): void {
    this.service.dashboard().subscribe({ next: dashboard => this.data.set(dashboard), error: () => this.logout() });
  }
}
