import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { CardApplicationDetail } from '../card-application.models';
import { CardApplicationApiService } from '../card-application-api.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-application-detail', imports: [CurrencyPipe, DatePipe, RouterLink],
  template: `<section class="page">@if (detail(); as item) { <p class="eyebrow">BAŞVURU DETAYI</p><h1>{{ item.application.applicationNumber }}</h1><div class="summary"><div><span>Müşteri</span><b>{{ item.application.customerFullName }}</b></div><div><span>Kart tipi</span><b>{{ item.application.cardTypeName }}</b></div><div><span>Talep edilen limit</span><b>{{ item.application.requestedLimit | currency:'TRY':'symbol-narrow':'1.2-2' }}</b></div><div><span>Durum</span><b>{{ item.application.status }}</b></div></div>@if(item.evaluationNote){<article class="note"><b>Değerlendirme notu</b><p>{{item.evaluationNote}}</p></article>}@if(item.creditCardId){<a class="card-link" [routerLink]="cardRoute(item.creditCardId)">Oluşturulan kartı görüntüle →</a>}<h2>Durum Zaman Çizgisi</h2><ol>@for(history of item.history;track history.changedAtUtc){<li><i></i><div><b>{{history.newStatus}}</b><span>{{history.changedAtUtc | date:'dd.MM.yyyy HH:mm'}} · {{history.changedBy}}</span>@if(history.description){<p>{{history.description}}</p>}</div></li>}</ol> } @else { <p>{{error() || 'Başvuru yükleniyor...'}}</p> }</section>`,
  styles: `.page{padding:2rem}.eyebrow{color:#b88918;font-size:.75rem;font-weight:700;letter-spacing:.08em}h1,h2{color:#102a43}.summary{display:grid;grid-template-columns:repeat(4,1fr);border:1px solid #d9e2ec;background:#d9e2ec;gap:1px}.summary div{background:#fff;padding:1rem;display:grid;gap:.35rem}.summary span{font-size:.78rem;color:#627d98}.note{margin:1rem 0;padding:1rem;border-left:4px solid #b88918;background:#fffaf0}.card-link{display:inline-block;margin:1rem 0;color:#8a6817;font-weight:700}ol{list-style:none;margin:0;padding:0}li{display:flex;gap:1rem;padding:0 0 1.25rem}li i{width:12px;height:12px;background:#b88918;border-radius:50%;margin-top:.3rem}li div{display:grid;gap:.25rem}li span{color:#627d98;font-size:.85rem}li p{margin:.2rem 0}`
})
export class ApplicationDetail implements OnInit {
  protected readonly detail = signal<CardApplicationDetail | null>(null); protected readonly error = signal('');
  constructor(private readonly route: ActivatedRoute, private readonly api: CardApplicationApiService, private readonly authService: AuthService) {}
  ngOnInit(): void { const id=Number(this.route.snapshot.paramMap.get('id')); if(!id){this.error.set('Geçersiz başvuru numarası.');return;} this.api.getDetail(id).subscribe({next:data=>this.detail.set(data),error:()=>this.error.set('Başvuru detayına erişilemedi.')}); }
  protected cardRoute(id: number): string[] { return [this.authService.currentUser()?.role === 'Manager' ? '/manager/cards' : '/officer/cards', String(id)]; }
}
