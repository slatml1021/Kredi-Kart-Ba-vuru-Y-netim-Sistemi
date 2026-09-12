import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CardApiService, LimitIncreaseRequest } from '../card-api.service';

@Component({
  selector: 'app-limit-increase-review',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink],
  template: `
    <section class="page">
      <header><p>KART YÖNETİMİ</p><h1>Limit Değişiklik Talepleri</h1><span>Kart limit artırım ve azaltım taleplerini tek kuyrukta değerlendirin.</span></header>
      @if (message()) { <div class="message">{{ message() }}</div> }
      <article>
        <div class="filters">
          <button type="button" [class.active]="filter() === 'Pending'" (click)="filter.set('Pending')">Bekleyenler</button>
          <button type="button" [class.active]="filter() === 'All'" (click)="filter.set('All')">Tümü</button>
        </div>
        <div class="table-wrap"><table>
          <thead><tr><th>MÜŞTERİ</th><th>KART</th><th>TÜR</th><th>MEVCUT LİMİT</th><th>TALEP</th><th>TARİH</th><th>DURUM</th><th>İŞLEM</th></tr></thead>
          <tbody>
            @for (item of visibleRequests(); track item.cardId) {
              <tr>
                <td><b>{{ item.customerFullName }}</b><small>{{ item.customerNumber }}</small></td>
                <td>{{ item.cardTypeName }}<small>{{ item.maskedCardNumber }}</small></td>
                <td><b>{{ item.changeType === 'Decrease' ? 'Azaltım' : 'Artırım' }}</b></td>
                <td>{{ item.currentLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</td>
                <td><b>{{ item.requestedNewLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></td>
                <td>{{ item.requestedAtUtc | date:'dd.MM.yyyy HH:mm' }}</td>
                <td><span class="badge" [class.done]="item.status !== 'Pending'">{{ statusLabel(item.status) }}</span></td>
                <td>@if (item.status === 'Pending') { <button type="button" (click)="selected.set(item); note.setValue('')">Değerlendir</button> } @else { {{ item.evaluationNote || '—' }} }</td>
              </tr>
            } @empty { <tr><td colspan="8" class="empty">Bu filtrede limit değişiklik talebi bulunmuyor.</td></tr> }
          </tbody>
        </table></div>
      </article>
      @if (selected(); as item) {
        <div class="overlay" (click)="selected.set(null)">
          <section class="modal" (click)="$event.stopPropagation()">
            <h2>Limit {{ item.changeType === 'Decrease' ? 'Azaltımını' : 'Artırımını' }} Değerlendir</h2>
            <p>{{ item.customerFullName }} için {{ item.currentLimit | currency:'TRY':'symbol-narrow':'1.0-0' }} → <b>{{ item.requestedNewLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></p>
            <a class="card-link" [routerLink]="['/manager/cards', item.cardId]">Kart bilgilerini ayrıca görüntüle →</a>
            <label>Değerlendirme Notu<textarea rows="4" [formControl]="note" placeholder="Onay için isteğe bağlı, red için zorunlu"></textarea></label>
            <div><button class="cancel" type="button" (click)="selected.set(null)">Vazgeç</button><button class="reject" type="button" (click)="evaluate('Rejected')">Reddet</button><button class="approve" type="button" (click)="evaluate('Approved')">Onayla</button></div>
          </section>
        </div>
      }
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1240px;margin:auto;padding:30px;color:#24384e}header p{margin:0;color:#9b741c;font-size:10px;font-weight:900;letter-spacing:.15em}header h1{margin:6px 0;color:#0b2a4d;font-size:30px}header span{color:#718093}article{margin-top:24px;border:1px solid #dce3e9;border-radius:10px;background:#fff;overflow:hidden}.filters{display:flex;gap:8px;padding:14px}.filters button,td button{border:1px solid #cad5df;border-radius:7px;background:#fff;padding:8px 12px;font-weight:700;color:#173653}.filters .active,td button{color:#fff;background:#0b2a4d}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:13px;text-align:left;border-top:1px solid #e7ebef;font-size:12px}th{color:#7a8897;font-size:9px;letter-spacing:.08em}td small{display:block;color:#8290a0;margin-top:4px}.badge{padding:5px 9px;border-radius:20px;background:#fff1ce;color:#8a6100}.badge.done{background:#e8f3ee;color:#19734e}.empty{text-align:center;color:#7b8896}.message{margin-top:16px;padding:12px;border-radius:8px;background:#edf8f2;color:#18744e}.overlay{position:fixed;inset:0;z-index:20;display:grid;place-items:center;background:#0b1d31aa}.modal{width:min(520px,calc(100vw - 30px));padding:24px;border-radius:12px;background:#fff}.modal h2{margin-top:0}.card-link{display:inline-block;margin:-4px 0 15px;color:#174d78;font-weight:800;text-decoration:none}.modal label{display:grid;gap:7px}.modal textarea{padding:11px;border:1px solid #cbd5df;border-radius:7px}.modal div{display:flex;justify-content:flex-end;gap:8px;margin-top:16px}.modal button{border:0;border-radius:7px;padding:10px 14px;color:#fff;font-weight:800}.modal .cancel{color:#24384e;background:#edf1f4}.modal .reject{background:#b64046}.modal .approve{background:#18744e}
  `,
})
export class LimitIncreaseReview implements OnInit {
  protected readonly requests = signal<LimitIncreaseRequest[]>([]);
  protected readonly selected = signal<LimitIncreaseRequest | null>(null);
  protected readonly filter = signal<'Pending' | 'All'>('Pending');
  protected readonly message = signal('');
  protected readonly note = new FormControl('', { nonNullable: true });
  private readonly requestedCardId: number;
  constructor(private readonly api: CardApiService, route: ActivatedRoute) {
    this.requestedCardId = Number(route.snapshot.paramMap.get('cardId'));
  }
  ngOnInit(): void { this.load(); }
  protected visibleRequests(): LimitIncreaseRequest[] {
    return this.filter() === 'All' ? this.requests() : this.requests().filter(item => item.status === 'Pending');
  }
  protected statusLabel(value: string): string {
    return ({ Pending: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' } as Record<string, string>)[value] ?? value;
  }
  protected evaluate(decision: 'Approved' | 'Rejected'): void {
    const item = this.selected();
    if (!item) return;
    if (decision === 'Rejected' && !this.note.value.trim()) {
      this.message.set('Red kararı için açıklama girin.');
      return;
    }
    this.api.evaluateLimitIncrease(item.cardId, decision, this.note.value).subscribe({
      next: () => {
        this.selected.set(null);
        this.message.set(decision === 'Approved' ? 'Limit değişiklik talebi onaylandı.' : 'Limit değişiklik talebi reddedildi.');
        this.load();
      },
      error: response => this.message.set(response.error?.detail ?? 'Limit değişiklik talebi değerlendirilemedi.'),
    });
  }
  private load(): void {
    this.api.getLimitIncreases().subscribe({
      next: items => {
        this.requests.set(items);
        if (this.requestedCardId > 0) {
          const requested = items.find(item => item.cardId === this.requestedCardId);
          if (requested?.status === 'Pending') this.selected.set(requested);
          else if (requested) { this.filter.set('All'); this.message.set('Bu limit talebi daha önce sonuçlandırılmıştır.'); }
          else this.message.set('İncelenecek limit talebi bulunamadı.');
        }
      },
      error: () => this.message.set('Limit değişiklik talepleri yüklenemedi.'),
    });
  }
}
