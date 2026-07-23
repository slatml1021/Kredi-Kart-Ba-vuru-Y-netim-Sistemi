import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { CardApiService, CreditCard } from '../card-api.service';

@Component({
  selector: 'app-card-detail', imports: [CurrencyPipe, DatePipe],
  template: `<section class="page">@if (card(); as item) { <p class="eyebrow">KART DETAYI</p><h1>{{ item.cardTypeName }} Kart</h1><div class="credit-card"><span>{{ item.cardTypeName }}</span><strong>{{ item.maskedCardNumber }}</strong><small>{{ item.customerFullName }}</small><small>{{ item.expiryDate | date:'MM/yy' }}</small></div><div class="details"><div><span>Başvuru</span><b>{{ item.applicationNumber }}</b></div><div><span>Kart limiti</span><b>{{ item.cardLimit | currency:'TRY':'symbol-narrow':'1.2-2' }}</b></div><div><span>Durum</span><b>{{ item.status }}</b></div><div><span>Veriliş tarihi</span><b>{{ item.issueDate | date:'dd.MM.yyyy' }}</b></div></div> } @else { <p>{{ error() || 'Kart bilgisi yükleniyor...' }}</p> }</section>`,
  styles: `.page{padding:2rem}.eyebrow{color:#b88918;font-size:.75rem;font-weight:700;letter-spacing:.08em}h1{color:#102a43}.credit-card{max-width:420px;min-height:210px;border-radius:16px;padding:1.5rem;display:grid;background:linear-gradient(130deg,#102a43,#243b53);color:#fff;box-shadow:0 12px 24px #243b5340}.credit-card strong{font-size:1.5rem;letter-spacing:.08em}.credit-card small{align-self:end}.details{margin-top:1.5rem;max-width:600px;display:grid;grid-template-columns:1fr 1fr;gap:1px;background:#d9e2ec;border:1px solid #d9e2ec}.details div{background:#fff;padding:1rem;display:grid;gap:.4rem}.details span{color:#627d98;font-size:.8rem}`
})
export class CardDetail implements OnInit {
  protected readonly card = signal<CreditCard | null>(null); protected readonly error = signal('');
  constructor(private readonly route: ActivatedRoute, private readonly api: CardApiService) {}
  ngOnInit(): void { const id = Number(this.route.snapshot.paramMap.get('id')); if (!id) { this.error.set('Geçersiz kart numarası.'); return; } this.api.getById(id).subscribe({ next: card => this.card.set(card), error: () => this.error.set('Kart bilgisine erişilemedi.') }); }
}
