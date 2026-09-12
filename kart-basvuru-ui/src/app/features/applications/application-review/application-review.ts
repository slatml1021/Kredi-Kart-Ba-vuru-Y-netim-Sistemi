import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CardApplicationApiService } from '../card-application-api.service';
import { CardApplication } from '../card-application.models';

type PeriodFilter = 'All' | 'Today' | 'Week';
type SortOrder = 'Newest' | 'Oldest' | 'LimitDesc' | 'LimitAsc' | 'Customer';

@Component({
  selector: 'app-application-review',
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="page">
      <p>BAŞVURU YÖNETİMİ</p>
      <h1>{{ showAll ? 'Tüm Başvurular' : 'Bekleyen Başvurular' }}</h1>
      <span>{{ showAll ? 'Sistemdeki bütün başvuruları ve sonuçlarını görüntüleyin.' : 'Değerlendirme bekleyen başvuruları kuyruk sırasıyla inceleyin.' }}</span>
      @if (error()) { <div class="error" role="alert"><span>{{ error() }}</span><button type="button" aria-label="Uyarıyı kapat" (click)="error.set('')">×</button></div> }

      <div class="card">
        <div class="filters">
          <input type="search" [value]="searchTerm()" (input)="searchTerm.set($any($event.target).value)" placeholder="Başvuru no, müşteri veya kart tipi ara…">
          <select aria-label="Başvuru durumu" [value]="statusFilter()" (change)="statusFilter.set($any($event.target).value)">
            <option value="All">Tüm durumlar</option><option value="Pending">Beklemede</option>
            <option value="Revision">Revizyonda</option><option value="Approved">Onaylandı</option>
            <option value="Rejected">Reddedildi</option>
          </select>
          <select aria-label="Başvuruları sırala" [value]="sortOrder()" (change)="sortOrder.set($any($event.target).value)">
            <option value="Newest">En yeni başvuru</option><option value="Oldest">En eski başvuru</option>
            <option value="LimitDesc">Limit: yüksekten düşüğe</option><option value="LimitAsc">Limit: düşükten yükseğe</option>
            <option value="Customer">Müşteri adına göre</option>
          </select>
          <div class="periods">
            <button type="button" [class.active]="period()==='All'" (click)="period.set('All')">Tümü</button>
            <button type="button" [class.active]="period()==='Today'" (click)="period.set('Today')">Bugün</button>
            <button type="button" [class.active]="period()==='Week'" (click)="period.set('Week')">Bu Hafta</button>
          </div>
        </div>
        <div class="list-meta">
          <span>{{ visibleApplications().length }} / {{ filteredApplications().length }} kayıt listeleniyor</span>
          <label>Sayfa boyutu
            <select [value]="pageSize()" (change)="pageSize.set(+$any($event.target).value)">
              <option [value]="10">10</option><option [value]="25">25</option><option [value]="50">50</option>
            </select>
          </label>
        </div>
        <div class="table-wrap"><table>
          <thead><tr><th>BAŞVURU NO</th><th>MÜŞTERİ</th><th>KART TİPİ</th><th>HESAPLANAN LİMİT</th><th>SORUMLU</th><th>BAŞVURU TARİHİ</th><th>DURUM</th><th>İŞLEM</th></tr></thead>
          <tbody>
            @for (item of visibleApplications(); track item.id) {
              <tr>
                <td><b>{{ item.applicationNumber }}</b></td><td>{{ item.customerFullName }}</td>
                <td>{{ item.cardTypeName }}</td><td>{{ item.availableLimit | number:'1.0-0' }} TL</td><td>{{ item.assignedOfficerName || 'Atanmamış' }}</td>
                <td>{{ item.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</td>
                <td><span [class]="item.status.toLowerCase()">● {{ statusLabel(item.status) }}@if(item.isEvaluationBlocked){ · Önceki başvuru bekleniyor}@if(item.workflowStage === 'SecondManagerApproval'){ · 2. Onay}</span></td>
                <td><a [routerLink]="['/manager/applications', item.id]">İncele</a></td>
              </tr>
            } @empty { <tr><td colspan="8" class="empty">Filtreye uygun başvuru yok.</td></tr> }
          </tbody>
        </table></div>
      </div>
    </section>
  `,
  styles: `
    .page{padding:2rem}.page>p{color:#b88918;font-weight:700;font-size:.75rem;letter-spacing:.08em}.page>span{color:#627d98}h1{color:#102a43}.card{overflow:hidden;background:#fff;border:1px solid #d9e2ec;border-radius:10px;margin-top:1rem}.filters{display:grid;grid-template-columns:minmax(260px,1fr) 170px 210px auto;gap:.7rem;padding:1rem;border-bottom:1px solid #e2e8ee}.filters input,.filters select,.list-meta select{padding:.7rem;border:1px solid #cbd5df;border-radius:7px;background:#fff}.periods{display:flex}.filters button{padding:.65rem .75rem;border:1px solid #d5dde5;color:#4a5f73;background:#fff}.filters button.active{color:#fff;background:#0b2a4d}.list-meta{display:flex;justify-content:space-between;align-items:center;padding:.65rem 1rem;color:#627d98;font-size:.75rem;background:#f8fafc}.list-meta label{display:flex;align-items:center;gap:.5rem}.list-meta select{padding:.35rem}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:.85rem;border-bottom:1px solid #edf2f7;text-align:left;white-space:nowrap}th{color:#718093;font-size:.68rem}td{font-size:.8rem}td>b{color:#174d78}td a{color:#0b3d6d;font-weight:800;text-decoration:none}td span{display:inline-block;padding:.35rem .55rem;border-radius:999px;font-size:.7rem;font-weight:800}.pending{color:#1e5a93;background:#e8f1fb}.revision{color:#805b00;background:#fff1cf}.approved{color:#18734d;background:#e1f5eb}.rejected{color:#a52b2b;background:#fde7e7}.error{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:1rem;padding:10px 12px;border-radius:7px;color:#b42318;background:#fff0f0}.error button{border:0;color:inherit;background:transparent;font-size:20px;cursor:pointer}.empty{text-align:center;color:#627d98;padding:2rem}@media(max-width:1050px){.filters{grid-template-columns:1fr 1fr}.periods{justify-content:flex-end}}@media(max-width:700px){.page{padding:1.25rem}.filters{grid-template-columns:1fr}.periods{justify-content:stretch}.periods button{flex:1}.list-meta{align-items:flex-start;flex-direction:column;gap:.5rem}}
  `,
})
export class ApplicationReview implements OnInit {
  protected readonly showAll: boolean;
  protected readonly applications = signal<CardApplication[]>([]);
  protected readonly error = signal('');
  protected readonly searchTerm = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly period = signal<PeriodFilter>('All');
  protected readonly sortOrder = signal<SortOrder>('Newest');
  protected readonly pageSize = signal(10);
  protected readonly filteredApplications = computed(() => {
    const term = this.searchTerm().trim().toLocaleLowerCase('tr-TR');
    const now = new Date();
    const start = this.period() === 'Today'
      ? new Date(now.getFullYear(), now.getMonth(), now.getDate())
      : new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6);
    const result = this.applications().filter(item => {
      const haystack = `${item.applicationNumber} ${item.customerFullName} ${item.customerNumber} ${item.cardTypeName}`
        .toLocaleLowerCase('tr-TR');
      const matchesTerm = !term || haystack.includes(term);
      const matchesPeriod = this.period() === 'All' || new Date(item.createdAtUtc) >= start;
      const matchesStatus = this.statusFilter() === 'All' || item.status === this.statusFilter();
      return matchesTerm && matchesPeriod && matchesStatus;
    });
    return result.sort((left, right) => {
      switch (this.sortOrder()) {
        case 'Oldest': return +new Date(left.createdAtUtc) - +new Date(right.createdAtUtc);
        case 'LimitDesc': return right.availableLimit - left.availableLimit;
        case 'LimitAsc': return left.availableLimit - right.availableLimit;
        case 'Customer': return left.customerFullName.localeCompare(right.customerFullName, 'tr');
        default: return +new Date(right.createdAtUtc) - +new Date(left.createdAtUtc);
      }
    });
  });
  protected readonly visibleApplications = computed(() => this.filteredApplications().slice(0, this.pageSize()));

  constructor(private readonly api: CardApplicationApiService, route: ActivatedRoute) {
    this.showAll = route.snapshot.data['showAll'] === true;
    const requestedStatus = route.snapshot.queryParamMap.get('status');
    if (requestedStatus && ['All', 'Pending', 'Approved', 'Rejected', 'Revision'].includes(requestedStatus))
      this.statusFilter.set(requestedStatus);
    else if (!this.showAll) this.statusFilter.set('Pending');
  }

  ngOnInit(): void {
    this.load();
  }

  protected statusLabel(value: string): string {
    return ({
      Pending: 'Beklemede', Approved: 'Onaylandı',
      Rejected: 'Reddedildi', Revision: 'Revizyonda', Withdrawn: 'Geri Çekildi', Cancelled: 'İptal Edildi',
    } as Record<string, string>)[value] ?? value;
  }

  private load(): void {
    (this.showAll ? this.api.getAll() : this.api.getPending()).subscribe({
      next: items => this.applications.set(items),
      error: () => this.error.set('Başvurular yüklenemedi.'),
    });
  }
}
