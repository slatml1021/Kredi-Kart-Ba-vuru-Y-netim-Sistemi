import { CurrencyPipe, DatePipe, Location } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { PlatformApiService, SupplementaryApplication } from '../../core/services/platform-api.service';

@Component({
  selector: 'app-supplementary-application-detail',
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink],
  template: `
    <section class="page">
      <div class="heading"><div><p>EK KART YÖNETİMİ</p><h1>Ek Kart Başvuru Detayı</h1><span>Başvuru sahibi, ek kart sahibi, limit ve teslimat bilgilerini tek ekranda inceleyin.</span></div><div class="heading-actions">@if (application()?.maskedCardNumber) { <a href="#created-supplementary-card">Oluşturulan Ek Kartı Görüntüle</a> }<button type="button" (click)="goBack()">← Geldiğim Sayfaya Dön</button></div></div>
      @if (message()) { <div class="message">{{ message() }}<button type="button" (click)="message.set('')">×</button></div> }
      @if (loading()) { <article class="state">Başvuru yükleniyor...</article> }
      @if (application(); as item) {
        <article class="hero"><div><span>BAŞVURU NO</span><b>{{ item.applicationNumber }}</b></div><div><span>OLUŞTURULMA</span><b>{{ item.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</b></div><div><span>DURUM</span><b [class]="item.status.toLowerCase()">{{ statusLabel(item.status) }}</b></div></article>
        <article><h2>Taraflar ve Ana Kart</h2><div class="grid">
          <a [routerLink]="[customerPrefix(), 'customers', item.primaryCustomerId]"><span>ANA KART SAHİBİ</span><b>{{ item.primaryCustomer }} ↗</b></a>
          <a [routerLink]="[customerPrefix(), 'customers', item.holderCustomerId]"><span>EK KART SAHİBİ</span><b>{{ item.holderCustomer }} ↗</b></a>
          <a [routerLink]="[customerPrefix(), 'cards', item.primaryCreditCardId]"><span>ANA KART</span><b>Kart detayını görüntüle ↗</b></a>
          <div><span>YAKINLIK</span><b>{{ item.relationship }}</b></div><div><span>TALEP EDİLEN LİMİT</span><b>{{ item.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
        </div></article>
        <article><h2>Teslimat Bilgileri</h2><div class="grid"><div><span>TESLİMAT YÖNTEMİ</span><b>{{ deliveryLabel(item.deliveryMethod) }}</b></div><div class="wide"><span>TESLİMAT ADRESİ / ŞUBE</span><b>{{ item.deliveryAddress }}</b></div></div></article>
        @if (item.status === 'Approved') {
          @if (item.maskedCardNumber) {
            <article id="created-supplementary-card" class="created-card-section">
              <div class="created-card-heading"><div><p>OLUŞTURULAN KART</p><h2>Ek Kredi Kartı</h2></div><b [class]="'card-state ' + (item.cardStatus || 'Inactive').toLowerCase()">{{ cardStatusLabel(item.cardStatus) }}</b></div>
              <div class="supplementary-card-visual">
                <div><span class="card-brand"><i>KB</i>Kart Başvuru Sistemi</span><b>EK KART</b></div>
                <strong>{{ item.maskedCardNumber }}</strong>
                <div><span><small>KART SAHİBİ</small>{{ item.holderCustomer }}</span><span><small>PAYLAŞILAN LİMİT</small>{{ item.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</span></div>
              </div>
              <p class="card-help">Bu kart ana kartın limitini paylaşır. Üretim ve teslim durumu aşağıdaki süreç alanından takip edilir.</p>
            </article>
          }
          <article><h2>Üretim ve Teslim Süreci</h2><div class="grid">
            <div><span>KART DURUMU</span><b>{{ cardStatusLabel(item.cardStatus) }}</b></div>
            <div><span>GÜNCEL AŞAMA</span><b>{{ fulfillmentLabel(item.fulfillmentStatus) }}</b></div>
            <div><span>TAHMİNİ BASIM</span><b>{{ item.estimatedPrintAtUtc ? (item.estimatedPrintAtUtc | date:'dd.MM.yyyy HH:mm') : 'Planlanıyor' }}</b></div>
            <div><span>TAHMİNİ TESLİM</span><b>{{ item.estimatedDeliveryAtUtc ? (item.estimatedDeliveryAtUtc | date:'dd.MM.yyyy HH:mm') : 'Planlanıyor' }}</b></div>
            <div><span>TAKİP NUMARASI</span><b>{{ item.trackingNumber || 'Kuryeye verilmedi' }}</b></div>
            <div><span>TESLİM TARİHİ</span><b>{{ item.deliveredAtUtc ? (item.deliveredAtUtc | date:'dd.MM.yyyy HH:mm') : 'Henüz teslim edilmedi' }}</b></div>
          </div><p class="process-note">Üretim, basım ve teslim adımları sistem tarafından otomatik ilerletilir. Kart, teslim edildikten sonra ek kart sahibi tarafından müşteri portalından aktive edilir.</p></article>
        }
        <article><h2>Değerlendirme</h2><div class="grid"><div><span>DEĞERLENDİRME TARİHİ</span><b>{{ item.evaluatedAtUtc ? (item.evaluatedAtUtc | date:'dd.MM.yyyy HH:mm') : 'Henüz değerlendirilmedi' }}</b></div><div class="wide"><span>DEĞERLENDİRME NOTU</span><b>{{ item.evaluationNote || '—' }}</b></div>@if(item.maskedCardNumber){<div><span>OLUŞTURULAN EK KART</span><b>{{ item.maskedCardNumber }}</b></div>}</div>
          @if (isManager() && item.status === 'Pending') { <div class="actions"><button class="approve" type="button" (click)="openEvaluation('Approved')">Onayla</button><button class="reject" type="button" (click)="openEvaluation('Rejected')">Reddet</button></div> }
        </article>
      }
      @if (evaluationDecision()) { <div class="overlay" (click)="closeEvaluation()"><section class="modal" role="dialog" aria-modal="true" (click)="$event.stopPropagation()"><h2>{{ evaluationDecision() === 'Approved' ? 'Ek kart başvurusunu onayla' : 'Ek kart başvurusunu reddet' }}</h2><p>{{ evaluationDecision() === 'Approved' ? 'Onay sonrasında ek kart numarası oluşturulur ve limit ana karttan tahsis edilir.' : 'Ret nedeni zorunludur ve başvuru sahibine bildirilir.' }}</p><label>Değerlendirme notu {{ evaluationDecision() === 'Rejected' ? '*' : '(isteğe bağlı)' }}<textarea rows="4" [(ngModel)]="evaluationNote" placeholder="Karar açıklamasını yazın"></textarea></label><div><button type="button" class="secondary" (click)="closeEvaluation()">Vazgeç</button><button type="button" [class]="evaluationDecision() === 'Approved' ? 'approve' : 'reject'" [disabled]="evaluationDecision() === 'Rejected' && !evaluationNote.trim()" (click)="submitEvaluation()">{{ evaluationDecision() === 'Approved' ? 'Başvuruyu Onayla' : 'Başvuruyu Reddet' }}</button></div></section></div> }
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1120px;margin:auto;padding:32px;color:#20364d}.heading{display:flex;justify-content:space-between;align-items:end;margin-bottom:22px}.heading p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.15em}.heading h1{margin:7px 0 4px;color:#0b2a4d;font-size:31px}.heading span{color:#718093}.heading-actions{display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-end}.heading-actions a,.heading-actions button{padding:10px 14px;border:1px solid #c9d5df;border-radius:7px;color:#173d64;background:#fff;font-weight:800;text-decoration:none}.state,.message,article{margin-top:15px;padding:20px;border:1px solid #dce3e9;border-radius:11px;background:#fff}.message{display:flex;justify-content:space-between;color:#174d78;background:#edf6ff}.message button{border:0;color:inherit;background:transparent;font-size:20px}article h2{margin:0 0 14px;color:#17334f;font-size:17px}.hero{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;background:#f9fbfc}.hero>div,.grid>a,.grid>div{display:grid;gap:6px;padding:14px;border:1px solid #e0e6eb;border-radius:8px;background:#fff}.hero span,.grid span{color:#7a8998;font-size:9px;font-weight:800;letter-spacing:.06em}.hero b,.grid b{font-size:13px}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.grid>a{color:#173d64;text-decoration:none}.grid .wide{grid-column:span 2}.created-card-section{display:grid;grid-template-columns:minmax(320px,520px) 1fr;gap:18px;align-items:center;scroll-margin-top:20px;background:linear-gradient(135deg,#f8fafc,#fff)}.created-card-heading{grid-column:1/-1;display:flex;justify-content:space-between;align-items:center}.created-card-heading p{margin:0 0 5px;color:#a17c22;font-size:9px;font-weight:900;letter-spacing:.14em}.created-card-heading h2{margin:0}.card-state{padding:6px 10px;border-radius:999px;color:#8a6410;background:#fff3d8;font-size:11px}.card-state.active{color:#17734d;background:#e6f6ee}.card-state.blocked,.card-state.cancelled{color:#a52b2b;background:#fdebea}.supplementary-card-visual{min-height:205px;padding:24px;border-radius:17px;display:flex;flex-direction:column;justify-content:space-between;color:#fff;background:radial-gradient(circle at 80% 15%,#356d9c,#0b2a4d 58%,#06182c);box-shadow:0 17px 35px rgba(8,37,65,.22)}.supplementary-card-visual>div{display:flex;justify-content:space-between;gap:15px}.supplementary-card-visual strong{font-size:22px;letter-spacing:.13em}.supplementary-card-visual small{display:block;margin-bottom:5px;color:#d7e1e9;font-size:8px}.card-brand{display:flex;align-items:center;gap:7px}.card-brand i{width:28px;height:28px;display:grid;place-items:center;border:1px solid #ffffff88;border-radius:50%;font-style:normal;font-weight:900}.card-help{margin:0;color:#617487;line-height:1.6}.process-note{margin:14px 0 0;padding:12px;border-left:3px solid #c69b32;color:#627487;background:#fff9e8;font-size:13px;line-height:1.5}.pending{color:#8b6715}.approved{color:#17734d}.rejected{color:#a52b2b}.actions{display:flex;justify-content:flex-end;gap:9px;margin-top:16px}button{padding:10px 14px;border:0;border-radius:7px;color:#fff;font-weight:800;cursor:pointer}.approve{background:#17734d}.reject{background:#ae3838}.secondary{color:#40566c;background:#e9eef3}.overlay{position:fixed;inset:0;z-index:100;display:grid;place-items:center;padding:20px;background:rgba(7,27,49,.65)}.modal{width:min(520px,100%);padding:24px;border-radius:12px;background:#fff}.modal p{color:#66798c;line-height:1.5}.modal label{display:grid;gap:7px;font-weight:800}.modal textarea{padding:11px;border:1px solid #cbd6df;border-radius:7px;font:inherit;resize:vertical}.modal>div{display:flex;justify-content:flex-end;gap:9px;margin-top:15px}@media(max-width:700px){.page{padding:20px}.heading{align-items:flex-start;flex-direction:column;gap:14px}.heading-actions{justify-content:flex-start}.hero,.grid,.created-card-section{grid-template-columns:1fr}.grid .wide{grid-column:auto}}
  `,
})
export class SupplementaryApplicationDetail implements OnInit {
  protected readonly application = signal<SupplementaryApplication | null>(null);
  protected readonly loading = signal(true);
  protected readonly message = signal('');
  protected readonly evaluationDecision = signal<'Approved' | 'Rejected' | null>(null);
  protected evaluationNote = '';
  protected readonly isManager = () => this.auth.currentUser()?.role === 'Manager';
  protected readonly customerPrefix = () => this.isManager() ? '/manager' : '/officer';

  constructor(private readonly route: ActivatedRoute, private readonly api: PlatformApiService, private readonly auth: AuthService, private readonly location: Location) {}

  ngOnInit(): void { this.load(); }

  protected statusLabel(value: string): string { return ({ Pending: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' } as Record<string,string>)[value] ?? value; }
  protected goBack(): void { this.location.back(); }
  protected deliveryLabel(value: string): string { return ({ RegisteredAddress: 'Kayıtlı Adres', Branch: 'Şubeden Teslim', DifferentAddress: 'Farklı Adres' } as Record<string,string>)[value] ?? value; }
  protected cardStatusLabel(value: string): string { return ({ NotCreated: 'Henüz oluşturulmadı', Inactive: 'Pasif — aktivasyon bekliyor', Active: 'Aktif', Blocked: 'Blokeli', Cancelled: 'İptal edildi' } as Record<string,string>)[value] ?? value; }
  protected fulfillmentLabel(value: string | null): string { return ({ Production: 'Üretimde', Printing: 'Basımda', Shipped: 'Kuryede', Delivered: 'Teslim edildi' } as Record<string,string>)[value ?? ''] ?? 'Süreç başlamadı'; }
  protected openEvaluation(decision: 'Approved' | 'Rejected'): void { this.evaluationNote = ''; this.evaluationDecision.set(decision); }
  protected closeEvaluation(): void { this.evaluationDecision.set(null); this.evaluationNote = ''; }
  protected submitEvaluation(): void {
    const item = this.application(); const decision = this.evaluationDecision();
    if (!item || !decision || (decision === 'Rejected' && !this.evaluationNote.trim())) return;
    this.api.evaluateSupplementary(item.id, decision, this.evaluationNote.trim() || null).subscribe({
      next: updated => { this.application.set(updated); this.closeEvaluation(); this.message.set('Ek kart başvurusu kararı kaydedildi.'); },
      error: response => this.message.set(response.error?.detail ?? 'Karar kaydedilemedi.'),
    });
  }
  private load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(id) || id <= 0) { this.message.set('Geçersiz ek kart başvuru numarası.'); this.loading.set(false); return; }
    this.api.supplementaryById(id).subscribe({ next: item => { this.application.set(item); this.loading.set(false); }, error: response => { this.message.set(response.error?.detail ?? 'Ek kart başvurusu yüklenemedi.'); this.loading.set(false); } });
  }
}
