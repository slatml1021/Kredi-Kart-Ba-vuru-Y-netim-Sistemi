import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Location } from '@angular/common';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { CardApiService, CreditCard } from '../card-api.service';
import { Fulfillment, PlatformApiService, SupplementaryApplication } from '../../../core/services/platform-api.service';

@Component({
  selector: 'app-card-detail',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink],
  styleUrl: './card-detail-network.scss',
  template: `
    <section class="page">
        @if (card(); as item) {
        <header><div><p>KREDİ KARTI</p><h1>Kredi Kartı Detayı</h1><span>Onaylanan başvuru sonucunda oluşturulan kart kaydını görüntüleyin.</span></div><div class="pdf-actions"><button type="button" (click)="goBack()">← Geldiğim Sayfaya Dön</button>@if (auth.currentUser()?.role === 'Officer') { <a routerLink="/officer/applications/new" [queryParams]="{ type: 'supplementary', primaryCardId: item.id, cardLabel: item.cardTypeName + ' · ' + item.maskedCardNumber }">Ek Kart Başvurusu</a> }<button [disabled]="isPdfBusy()" (click)="downloadPdf(false)">PDF İndir</button><button [disabled]="isPdfBusy()" (click)="downloadPdf(true)">PDF'i E-postala</button></div></header>
        <div class="layout">
          <div class="visual-column">
            <div [class]="'credit-card ' + cardTheme(item.cardTypeName)"><div><span class="bank-brand"><i>KB</i>Kart Başvuru Sistemi</span><b>{{ productLabel(item.cardTypeName).toUpperCase() }}</b></div><div class="card-tech"><i class="chip"></i><svg class="contactless" viewBox="0 0 30 30" role="img" aria-label="Temassız ödeme"><path d="M8 11c3 2.3 3 5.7 0 8"/><path d="M13 7c5.8 4.5 5.8 11.5 0 16"/><path d="M18 3c8.5 6.7 8.5 17.3 0 24"/></svg></div><strong>{{ item.maskedCardNumber }}</strong><div><span><small>KART SAHİBİ</small>{{ item.customerFullName }}</span><span class="network"><small>SON KULLANMA</small><span class="expiry-network"><b>{{ item.expiryDate | date:'MM/yy' }}</b><img [src]="networkLogo(item.cardTypeName)" [alt]="networkLabel(item.cardTypeName) + ' kart ağı logosu'"></span></span></div></div>
            @if (auth.currentUser()?.role === 'Officer') {
              <section class="visual-limit">
                <h2>Limit Değişiklik Talebi</h2>
                <div class="limit-bounds"><span>Mevcut <b>{{ item.cardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></span><span>İzin verilen üst sınır <b>{{ item.customerMaximumLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></span></div>
                @if (item.limitIncreaseStatus === 'Pending') {
                  <div class="notice"><b>Limit artırım talebi değerlendirme bekliyor</b><p>Talep edilen yeni limit: {{ item.requestedNewLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</p></div>
                } @else {
                  <div class="limit-request"><select [formControl]="limitChangeType"><option value="Increase">Artırım</option><option value="Decrease">Azaltım</option></select><input type="number" min="1" step="500" [formControl]="requestedNewLimit" placeholder="Yeni limit"><button type="button" (click)="requestLimitChange()">Talep Oluştur</button></div>
                  <small>Artırım müdür onayına gider; azaltım kurallara uygunsa anında uygulanır.</small>
                }
              </section>
            }
          </div>
          <article>
            <h2>Kart Özeti</h2><div class="summary"><div><span>Başvuru No</span><b>{{ item.applicationNumber }}</b></div><div><span>Kart Tipi</span><b>{{ item.cardTypeName }}</b></div><div><span>Kart Durumu</span><b [class.passive]="item.status === 'Inactive'" [class.active-status]="item.status === 'Active'" [class.blocked-status]="item.status === 'Blocked' || item.status === 'Closed'">{{ statusLabel(item.status) }}</b></div></div>
            <h2>Kart Bilgileri</h2><div class="details"><div><span>Kart Numarası</span><b>{{ item.maskedCardNumber }}</b></div><div><span>Kart Sahibi</span><b>{{ item.customerFullName }}</b></div><div><span>Müşteri No</span><b>{{ item.customerNumber }}</b></div><div><span>Kart Tipi</span><b>{{ item.cardTypeName }}</b></div><div><span>Kredi Limiti</span><b>{{ item.cardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div><div><span>Kullanılabilir Limit</span><b>{{ item.cardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div><div><span>Son Kullanma Tarihi</span><b>{{ item.expiryDate | date:'MM/yy' }}</b></div><div><span>Oluşturulma Tarihi</span><b>{{ item.issueDate | date:'dd.MM.yyyy' }}</b></div></div>
            <div class="supplementary-heading"><div><h2>Bu Ana Karta Bağlı Ek Kartlar</h2><span>{{ approvedSupplementaryCount() }} üretilen · {{ supplementaryCards().length }} toplam kayıt</span></div>@if (auth.currentUser()?.role === 'Officer') { <a routerLink="/officer/applications/new" [queryParams]="{ type: 'supplementary', primaryCardId: item.id }">Yeni Ek Kart Başvurusu</a> }</div>
            <div class="supplementary-table"><table><thead><tr><th>EK KART SAHİBİ</th><th>YAKINLIK</th><th>EK KART NUMARASI</th><th>PAYLAŞILAN LİMİT</th><th>DURUM</th><th>OLUŞTURULMA</th><th>İŞLEM</th></tr></thead><tbody>
              @for (supplementary of supplementaryCards(); track supplementary.id) { <tr><td><b>{{ supplementary.holderCustomer }}</b></td><td>{{ supplementary.relationship }}</td><td>{{ supplementary.maskedCardNumber || 'Kart henüz üretilmedi' }}</td><td>{{ supplementary.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</td><td><span [class]="'supplementary-status ' + supplementary.status.toLowerCase()">{{ supplementaryStage(supplementary) }}</span></td><td>{{ (supplementary.issuedAtUtc || supplementary.createdAtUtc) | date:'dd.MM.yyyy HH:mm' }}</td><td><a [routerLink]="[auth.currentUser()?.role === 'Manager' ? '/manager/supplementary-applications' : '/officer/supplementary-applications', supplementary.id]">İncele →</a></td></tr> }
              @empty { <tr><td colspan="7" class="empty">Bu ana karta bağlı ek kart veya bekleyen ek kart başvurusu bulunmuyor.</td></tr> }
            </tbody></table></div>
            <h2>Kart Kullanım Tercihleri</h2><div class="details"><div><span>Ekstre Gönderim Tercihi</span><b>{{ statementLabel(item.statementPreference) }}</b></div><div><span>Temassız Kullanım</span><b>{{ item.contactlessEnabled ? 'Açık' : 'Kapalı' }}</b></div><div><span>İnternet Alışverişi</span><b>{{ item.internetShoppingEnabled ? 'Açık' : 'Kapalı' }}</b></div></div>
            <h2>Teslimat Bilgileri</h2><div class="details"><div><span>Teslimat Yöntemi</span><b>{{ deliveryLabel(item.deliveryMethod) }}</b></div><div><span>Teslimat Durumu</span><b>Üretim aşamasında</b></div><div class="wide"><span>Teslimat Adresi</span><b>{{ item.deliveryAddress }}</b></div></div>
            @if (fulfillment(); as flow) {
              <div class="fulfillment-head"><h2>Üretim · Basım · Teslim</h2><span>Tahmini basım: {{ flow.estimatedPrintAtUtc | date:'dd.MM.yyyy HH:mm' }} · Tahmini teslim: {{ flow.estimatedDeliveryAtUtc | date:'dd.MM.yyyy HH:mm' }}</span></div>
              <ol class="fulfillment">@for(step of flow.steps;track step.key){<li [class]="step.status.toLowerCase()"><i>{{step.status==='Completed'?'✓':step.status==='Active'?'●':'○'}}</i><b>{{step.label}}</b><small>{{step.timestampUtc ? (step.timestampUtc | date:'dd.MM HH:mm') : 'Otomatik ilerleyecek'}}</small></li>}</ol>
              @if(flow.trackingNumber){<p class="tracking">Takip No: <b>{{flow.trackingNumber}}</b></p>}
            }
            @if (actionMessage()) { <p class="action-message">{{ actionMessage() }}<button type="button" (click)="actionMessage.set('')" aria-label="Bildirimi kapat">×</button></p> }
            @if (item.status === 'Inactive') {
              <div class="notice"><b>Aktivasyon bekleniyor</b><p>Kart henüz müşteri tarafından aktive edilmediği için pasif durumdadır. Kart teslim edildikten sonra aktivasyon işlemi gerçekleştirilebilir.</p></div>
            } @else if (item.status === 'Active') {
              <div class="notice active-notice"><b>Kart aktif</b><p>Kart müşteri tarafından aktive edilmiştir ve mevcut kart kullanım tercihleri doğrultusunda kullanılabilir.</p></div>
            } @else {
              <div class="notice blocked-notice"><b>Kart kullanım durumu</b><p>{{ statusExplanation(item.status) }}</p></div>
            }
          </article>
        </div>
      } @else { <p>{{ error() || 'Kart bilgisi yükleniyor...' }}</p> }
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1240px;margin:0 auto;padding:30px;color:#263b51}header{display:flex;justify-content:space-between;align-items:flex-end;margin-bottom:24px}header p{margin:0 0 6px;color:#a17c22;font-size:10px;font-weight:800;letter-spacing:.15em}header h1{margin:0;color:#0b2a4d;font-size:30px}header span{display:block;margin-top:6px;color:#718093}.pdf-actions{display:flex;gap:8px;flex-wrap:wrap}.pdf-actions button,.pdf-actions a{padding:10px 13px;border:1px solid #c9d4df;border-radius:7px;color:#173b60;background:#fff;font-weight:800;text-decoration:none;font-size:12px}.pdf-actions button:disabled{opacity:.55}.layout{display:grid;grid-template-columns:minmax(330px,.75fr) minmax(480px,1.25fr);gap:0}.visual-column{padding:0 14px}.credit-card{min-height:230px;padding:24px;border-radius:17px;display:flex;flex-direction:column;justify-content:space-between;color:#fff;background:radial-gradient(circle at 80% 15%,#286291,#0a2542 56%,#06182c);box-shadow:0 20px 38px rgba(8,37,65,.25);overflow:hidden}.credit-card.gold{background:radial-gradient(circle at 80% 15%,#caa84c,#7b5412 58%,#342006)}.credit-card.platinum{background:radial-gradient(circle at 80% 15%,#8996a3,#303d49 58%,#111820)}.credit-card.troy{background:radial-gradient(circle at 80% 15%,#613b95,#1d285d 58%,#0b163d)}.credit-card>div{display:flex;justify-content:space-between}.bank-brand{display:flex!important;align-items:center;gap:7px}.bank-brand i{width:27px;height:27px;display:grid;place-items:center;border:1px solid #ffffff88;border-radius:50%;font-style:normal;font-weight:900}.network{text-align:right}.card-tech{align-items:center}.chip{width:42px;height:32px;border-radius:6px;background:linear-gradient(135deg,#d8b769,#f4df9e);box-shadow:inset 0 0 0 1px #9f803a}.contactless{width:32px;height:32px;fill:none;stroke:#fff;stroke-width:2.2;stroke-linecap:round}.credit-card>div:last-child span{display:grid;gap:5px}.credit-card small{color:#d7e1e9;font-size:9px}.credit-card strong{font-size:22px;letter-spacing:.14em}.visual-limit{margin-top:18px;padding:18px;border:1px solid #dce3e9;border-radius:11px;background:#fff}.visual-limit h2{margin:0 0 12px!important}.visual-limit>small{display:block;margin-top:10px;color:#718093;line-height:1.4}.limit-bounds{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:12px}.limit-bounds span{display:grid;gap:4px;padding:10px;border-radius:7px;background:#f4f7fa;color:#718093;font-size:10px}.limit-bounds b{color:#20364d;font-size:12px}.visual-limit .limit-request{display:grid;grid-template-columns:110px 1fr}.visual-limit .limit-request button{grid-column:1/-1}.layout article{padding:22px;border:1px solid #dce3e9;border-radius:10px;background:#fff}.layout h2{margin:22px 0 12px;color:#1b3249;font-size:15px}.layout h2:first-child{margin-top:0}.summary,.details{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.summary div,.details div{min-height:64px;padding:13px;border:1px solid #e0e6eb;border-radius:7px;background:#f8fafb;display:grid;gap:6px}.summary span,.details span{color:#8290a0;font-size:9px}.summary b,.details b{color:#1e344b;font-size:11px}.details .wide{grid-column:span 2}.passive,.active-status,.blocked-status{width:max-content;padding:5px 8px;border-radius:999px}.passive{color:#805b00!important;background:#fff1cf}.active-status{color:#17734d!important;background:#e4f5ec}.blocked-status{color:#a52b2b!important;background:#fdebea}.supplementary-heading{display:flex;justify-content:space-between;align-items:end;margin-top:22px}.supplementary-heading h2{margin:0}.supplementary-heading span{display:block;margin-top:5px;color:#718093;font-size:10px}.supplementary-heading a,.supplementary-table a{padding:8px 10px;border:1px solid #c9d4df;border-radius:6px;color:#174d78;font-size:10px;font-weight:800;text-decoration:none}.supplementary-table{margin-top:10px;overflow:auto;border:1px solid #e0e6eb;border-radius:7px}.supplementary-table table{width:100%;border-collapse:collapse}.supplementary-table th,.supplementary-table td{padding:10px;border-bottom:1px solid #edf1f4;text-align:left;white-space:nowrap;font-size:10px}.supplementary-table th{color:#7b8997;background:#f8fafb;font-size:8px}.supplementary-table .empty{padding:25px;text-align:center;color:#718093}.supplementary-status{display:inline-flex;padding:4px 7px;border-radius:999px;color:#8a6410;background:#fff3d8;font-weight:800}.supplementary-status.approved{color:#17734d;background:#e7f6ef}.supplementary-status.rejected{color:#a52b2b;background:#fdebea}.fulfillment-head span{color:#718093;font-size:10px}.fulfillment{margin:12px 0;padding:0;display:grid;grid-template-columns:repeat(4,1fr);list-style:none}.fulfillment li{position:relative;display:grid;justify-items:center;gap:5px;color:#8996a3}.fulfillment li:after{content:'';position:absolute;left:58%;right:-42%;top:14px;height:2px;background:#dce3e9}.fulfillment li:last-child:after{display:none}.fulfillment i{z-index:1;width:28px;height:28px;display:grid;place-items:center;border-radius:50%;background:#e7ecf0;font-style:normal}.fulfillment .completed i,.fulfillment .active i{color:#fff;background:#18815a}.fulfillment .active i{background:#d1a83f}.fulfillment small{font-size:8px;text-align:center}.tracking{padding:9px;background:#f4f7fa}.limit-request{display:flex;gap:10px}.limit-request input,.limit-request select{padding:11px;border:1px solid #ccd5dd;border-radius:7px}.limit-request input{flex:1}.limit-request button{padding:11px 14px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}.action-message{display:flex;justify-content:space-between;align-items:center;padding:10px 12px;border-radius:6px;color:#174d78;background:#edf6ff}.action-message button{border:0;color:inherit;background:transparent;font-size:20px;cursor:pointer}.notice{margin-top:18px;padding:15px;border-left:4px solid #b88918;border-radius:6px;background:#fff9e9}.notice p{margin:5px 0;color:#667789;line-height:1.5}.active-notice{border-left-color:#18815a;background:#edf8f2}.active-notice b{color:#17734d}.blocked-notice{border-left-color:#b64046;background:#fff1f1}@media(max-width:900px){.layout{grid-template-columns:1fr}.visual-column{padding:0 0 18px}.credit-card{max-width:500px}}@media(max-width:620px){.page{padding:20px}header{align-items:flex-start;flex-direction:column;gap:14px}.summary,.details{grid-template-columns:1fr}.details .wide{grid-column:auto}.fulfillment{grid-template-columns:1fr 1fr}.fulfillment li:after{display:none}.supplementary-heading{align-items:flex-start;flex-direction:column;gap:10px}}
  `,
})
export class CardDetail implements OnInit {
  protected readonly card = signal<CreditCard | null>(null);
  protected readonly error = signal('');
  protected readonly actionMessage = signal('');
  protected readonly fulfillment = signal<Fulfillment | null>(null);
  protected readonly supplementaryCards = signal<SupplementaryApplication[]>([]);
  protected readonly approvedSupplementaryCount = computed(() => this.supplementaryCards().filter(x => x.status === 'Approved').length);
  protected readonly isPdfBusy = signal(false);
  protected readonly requestedNewLimit = new FormControl<number | null>(null, [Validators.required, Validators.min(1)]);
  protected readonly limitChangeType = new FormControl<'Increase' | 'Decrease'>('Increase', { nonNullable: true });
  private cardId = 0;
  constructor(private readonly route: ActivatedRoute, private readonly api: CardApiService, private readonly platformApi: PlatformApiService, protected readonly auth: AuthService, private readonly location: Location) {}
  ngOnInit(): void { this.cardId = Number(this.route.snapshot.paramMap.get('id')); if (!this.cardId) { this.error.set('Geçersiz kart numarası.'); return; } this.api.getById(this.cardId).subscribe({ next: card => this.card.set(card), error: () => this.error.set('Kart bilgisine erişilemedi.') }); this.platformApi.fulfillment(this.cardId).subscribe({next:flow=>this.fulfillment.set(flow)}); this.platformApi.supplementary().subscribe({ next: rows => this.supplementaryCards.set(rows.filter(x => x.primaryCreditCardId === this.cardId)) }); }
  protected statusLabel(value: string): string { return ({ Inactive: 'Pasif', Active: 'Aktif', Blocked: 'Blokeli', Closed: 'Kapalı' } as Record<string, string>)[value] ?? value; }
  protected statusExplanation(value: string): string {
    return ({ Blocked: 'Kart güvenlik veya operasyon kontrolü nedeniyle blokelidir. Kullanım yeniden açılmadan işlem yapılamaz.', Closed: 'Kart kapatılmıştır ve kullanıma açık değildir.' } as Record<string, string>)[value] ?? `Kartın güncel durumu: ${this.statusLabel(value)}.`;
  }
  protected statementLabel(value: string): string { return ({ Email: 'E-posta', Paper: 'Basılı Ekstre', Mobile: 'Mobil Bildirim' } as Record<string, string>)[value] ?? value; }
  protected deliveryLabel(value: string): string { return ({ RegisteredAddress: 'Kayıtlı Adrese Teslim', DifferentAddress: 'Farklı Adrese Teslim', Branch: 'Şubeden Teslim' } as Record<string, string>)[value] ?? value; }
  protected supplementaryStatus(value: string): string { return ({ Pending: 'Beklemede', Approved: 'Üretildi', Rejected: 'Reddedildi' } as Record<string, string>)[value] ?? value; }
  protected goBack(): void { this.location.back(); }
  protected networkLabel(name: string): string { const value = name.toUpperCase(); return value.includes('TROY') ? 'TROY' : value.includes('MASTER') || value.includes('WORLD') ? 'MASTERCARD' : 'VISA'; }
  protected networkLogo(name: string): string { const value = name.toUpperCase(); return value.includes('TROY') ? '/assets/card-networks/troy.svg' : value.includes('MASTER') || value.includes('WORLD') ? '/assets/card-networks/mastercard.svg' : '/assets/card-networks/visa.svg'; }
  protected productLabel(name: string): string { return name.replace(/\s+(Visa|Mastercard|World|TROY)$/i, ''); }
  protected cardTheme(name: string): string { const value=name.toLowerCase(); return value.includes('troy') ? 'troy' : value.includes('platinum') ? 'platinum' : value.includes('gold') ? 'gold' : 'classic'; }
  protected supplementaryStage(item: SupplementaryApplication): string {
    if (item.status !== 'Approved') return this.supplementaryStatus(item.status);
    const process = ({ Production: 'Üretimde', Printing: 'Basımda', Shipped: 'Kuryede', Delivered: 'Teslim edildi' } as Record<string, string>)[item.fulfillmentStatus ?? ''];
    if (item.cardStatus === 'Active') return 'Aktif';
    return process ?? 'Pasif — süreç hazırlanıyor';
  }
  protected requestLimitChange(): void {
    const card = this.card();
    const requestedLimit = this.requestedNewLimit.value;
    if (!card || !requestedLimit || this.requestedNewLimit.invalid) {
      this.actionMessage.set('Geçerli bir yeni limit girin.');
      return;
    }
    if (this.limitChangeType.value === 'Increase' && requestedLimit <= card.cardLimit) {
      this.actionMessage.set('Artırım talebi mevcut kart limitinden büyük olmalıdır.');
      return;
    }
    if (this.limitChangeType.value === 'Increase' && requestedLimit > card.customerMaximumLimit) {
      this.actionMessage.set(`Bu kart için talep edilebilecek üst sınır ${card.customerMaximumLimit.toLocaleString('tr-TR')} TL’dir.`);
      return;
    }
    if (this.limitChangeType.value === 'Decrease' && requestedLimit >= card.cardLimit) {
      this.actionMessage.set('Azaltım talebi mevcut kart limitinden küçük olmalıdır.');
      return;
    }
    this.api.requestLimitChange(card.id, this.limitChangeType.value, requestedLimit).subscribe({
      next: updated => {
        this.card.set(updated);
        this.actionMessage.set(this.limitChangeType.value === 'Decrease'
          ? 'Limit azaltımı otomatik onaylandı ve sorumlu memurun bildirim merkezine kaydedildi.'
          : 'Limit artırım talebi müdür değerlendirme kuyruğuna alındı.');
        this.requestedNewLimit.reset(null);
      },
      error: response => this.actionMessage.set(response.error?.detail ?? 'Limit değişiklik talebi oluşturulamadı.'),
    });
  }
  protected downloadPdf(email: boolean): void {
    this.isPdfBusy.set(true);
    this.platformApi.cardPdf(this.cardId, email).subscribe({
      next: blob => {
        this.isPdfBusy.set(false);
        if (email) {
          this.actionMessage.set('Kart bilgi PDF’i müşterinin kayıtlı e-posta adresine gönderildi.');
          return;
        }
        const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`kart-${this.cardId}.pdf`;a.click();URL.revokeObjectURL(url);
      },
      error: response => {
        this.isPdfBusy.set(false);
        this.actionMessage.set(response.error?.detail ?? 'Kart PDF’i oluşturulamadı veya e-posta gönderilemedi.');
      },
    });
  }
}
