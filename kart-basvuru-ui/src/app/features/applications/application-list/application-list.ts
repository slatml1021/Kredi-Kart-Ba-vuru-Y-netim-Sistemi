import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PlatformApiService, SupplementaryApplication } from '../../../core/services/platform-api.service';
import { CardApiService, LimitIncreaseRequest } from '../../cards/card-api.service';
import { CardApplicationApiService } from '../card-application-api.service';
import { CardApplication } from '../card-application.models';
import { AuthService } from '../../../core/services/auth.service';

// Farklı operasyon kayıtlarını aynı ekranda güvenli biçimde ayrıştırır.
type RequestType = 'CardApplication' | 'SupplementaryCard' | 'LimitIncrease' | 'LimitDecrease';

interface UnifiedApplication {
  key: string; id: number; applicationNumber: string; requestType: RequestType;
  customerId: number | null; customerFullName: string; productLabel: string;
  requestedLimit: number; createdAtUtc: string; status: string;
  detailLink: (string | number)[]; canRevise: boolean;
}

@Component({
  selector: 'app-application-list',
  imports: [DecimalPipe, RouterLink],
  template: `
    <section class="page">
      <div class="page-heading">
        <div><p>{{ isManager() ? 'MÜDÜR PANELİ' : 'MEMUR PANELİ' }} / BAŞVURU YÖNETİMİ</p><h1>Tüm Başvurular</h1><span>Kredi kartı, ek kart ve limit değişikliği taleplerini tek ekrandan izleyin.</span></div>
        @if (!isManager()) { <a routerLink="/officer/applications/new">+ Yeni Başvuru</a> }
      </div>
      @if (error()) { <div class="error" role="alert"><span>{{ error() }}</span><button type="button" (click)="error.set('')" aria-label="Uyarıyı kapat">×</button></div> }

      <div class="request-summary" aria-label="Başvuru türü özeti">
        @for (item of requestTypeOptions.slice(1); track item.value) {
          <button type="button" [class.active]="requestTypeFilter() === item.value" (click)="toggleRequestType(item.value)">
            <span>{{ item.label }}</span><b>{{ requestTypeCount(item.value) }}</b>
          </button>
        }
      </div>

      <div class="filters">
        <input type="search" placeholder="Başvuru no, müşteri veya kart ara" [value]="search()" (input)="search.set($any($event.target).value)">
        <select aria-label="Başvuru türü" [value]="requestTypeFilter()" (change)="requestTypeFilter.set($any($event.target).value)">@for (item of requestTypeOptions; track item.value) { <option [value]="item.value">{{ item.label }}</option> }</select>
        <select aria-label="Durum" [value]="statusFilter()" (change)="statusFilter.set($any($event.target).value)">@for (item of statusOptions; track item.value) { <option [value]="item.value">{{ item.label }}</option> }</select>
        <div class="limit-range"><input type="number" min="0" aria-label="Minimum tutar" placeholder="Min. tutar" [value]="minimumLimit()" (input)="minimumLimit.set($any($event.target).value)"><input type="number" min="0" aria-label="Maksimum tutar" placeholder="Maks. tutar" [value]="maximumLimit()" (input)="maximumLimit.set($any($event.target).value)"></div>
        <select aria-label="Sıralama" [value]="sortOrder()" (change)="sortOrder.set($any($event.target).value)"><option value="Newest">En yeni</option><option value="Oldest">En eski</option><option value="LimitDesc">Tutar: yüksekten düşüğe</option><option value="LimitAsc">Tutar: düşükten yükseğe</option></select>
        <select aria-label="Tarih" [value]="dateFilter()" (change)="dateFilter.set($any($event.target).value)"><option value="All">Tüm tarihler</option><option value="Today">Bugün</option><option value="Week">Bu hafta</option></select>
        @if (filtersActive()) { <button type="button" class="clear" (click)="clearFilters()">Temizle</button> }
      </div>

      <div class="card"><div class="table-wrap"><table>
        <thead><tr><th>BAŞVURU NO</th><th>TÜR</th><th>MÜŞTERİ</th><th>ÜRÜN / İŞLEM</th><th>TALEP / LİMİT</th><th>TARİH</th><th>DURUM</th><th>İŞLEM</th></tr></thead>
        <tbody>
          @for (application of filteredApplications(); track application.key) {
            <tr>
              <td><b>{{ application.applicationNumber }}</b></td>
              <td><span [class]="requestTypeClass(application.requestType)">{{ requestTypeLabel(application.requestType) }}</span></td>
              <td>@if (application.customerId) { <a class="customer-link" [routerLink]="[isManager() ? '/manager/customers' : '/officer/customers', application.customerId]">{{ application.customerFullName }}</a> } @else { <span class="customer-name">{{ application.customerFullName }}</span> }</td>
              <td>{{ application.productLabel }}</td><td>{{ application.requestedLimit | number:'1.2-2' }} TL</td><td>{{ formatDate(application.createdAtUtc) }}</td>
              <td><span [class]="statusClass(application.status)">● {{ statusLabel(application.status) }}</span></td>
              <td class="actions"><a [routerLink]="application.detailLink">İncele</a>@if (application.canRevise) { <a [routerLink]="application.detailLink">Revize Et</a> }</td>
            </tr>
          } @empty { <tr><td colspan="8" class="empty">Filtreye uygun başvuru veya talep bulunmuyor.</td></tr> }
        </tbody>
      </table></div></div>
    </section>
  `,
  styles: `
    :host{display:block}.page{box-sizing:border-box;width:100%;max-width:1500px;margin:0 auto;padding:30px;color:#25384d;font-size:14px}.page-heading{display:flex;justify-content:space-between;align-items:flex-end;margin-bottom:20px}.page-heading p{margin:0 0 8px;color:#9a741a;font-size:11px;font-weight:800;letter-spacing:.12em}.page-heading h1{margin:0;color:#0b2a4d;font-size:30px}.page-heading span{display:block;margin-top:7px;color:#6b7b8c;font-size:13px}.page-heading>a{padding:11px 15px;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800;text-decoration:none}.request-summary{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin-bottom:12px}.request-summary button{display:flex;align-items:center;justify-content:space-between;padding:12px 14px;border:1px solid #dce3ea;border-radius:9px;color:#496078;background:#fff;cursor:pointer}.request-summary button.active{border-color:#b98e27;background:#fff9e9;box-shadow:0 0 0 1px #b98e27}.request-summary span{font-size:12px;font-weight:800}.request-summary b{display:grid;min-width:28px;height:28px;place-items:center;border-radius:999px;color:#174d78;background:#eef4fa;font-size:12px}.filters{display:grid;grid-template-columns:minmax(240px,1.4fr) repeat(2,minmax(150px,.75fr)) minmax(230px,1fr) minmax(190px,.9fr) minmax(140px,.7fr) auto;gap:10px;align-items:center;margin-bottom:16px;padding:14px;border:1px solid #dce3ea;border-radius:10px;background:#fff}.filters input,.filters select{box-sizing:border-box;min-width:0;height:42px;padding:0 13px;border:1px solid #cbd5df;border-radius:7px;background:#fff;font:inherit}.limit-range{display:grid;grid-template-columns:1fr 1fr;gap:7px}.filters .clear{height:42px;padding:0 12px;border:1px solid #d3dde7;border-radius:7px;color:#52677c;background:#f7f9fb;font-weight:800;cursor:pointer;white-space:nowrap}.card{overflow:hidden;border:1px solid #dce3ea;border-radius:12px;background:#fff;box-shadow:0 8px 24px rgba(11,42,77,.05)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:15px 14px;border-bottom:1px solid #edf1f4;text-align:left;white-space:nowrap}th{color:#718093;background:#f8fafc;font-size:10px;letter-spacing:.06em}td{font-size:12px}td>b{color:#174d78}.customer-link{color:#174d78;font-weight:800;text-decoration:none}.customer-link:hover{text-decoration:underline}.customer-name{font-weight:800}.request-type{display:inline-block;padding:6px 9px;border-radius:999px;font-size:10px;font-weight:800}.type-card{color:#174d78;background:#e8f1fb}.type-supplementary{color:#72540b;background:#fff0c8}.type-increase{color:#6b3d91;background:#f1e8fa}.type-decrease{color:#176b5c;background:#e4f4ef}.status{display:inline-block;padding:6px 9px;border-radius:999px;font-size:10px;font-weight:800}.pending{background:#e8f1fb;color:#1e5a93}.revision{background:#fff1cf;color:#805b00}.approved{background:#e1f5eb;color:#18734d}.rejected{background:#fde7e7;color:#a52b2b}.withdrawn,.cancelled{background:#edf0f3;color:#5d6d7c}.actions{display:flex;gap:7px}.actions a{padding:7px 10px;border:1px solid #174d78;border-radius:6px;color:#174d78;background:#fff;font-size:11px;font-weight:800;text-decoration:none}.error{margin-bottom:15px;padding:12px;display:flex;align-items:center;justify-content:space-between;border-radius:7px;color:#8a5a10;background:#fff4d8}.error button{border:0;color:inherit;background:transparent;font-size:20px;cursor:pointer}.empty{text-align:center;color:#718093;padding:32px}@media(max-width:1250px){.filters{grid-template-columns:repeat(3,1fr)}.filters>input:first-child{grid-column:span 2}}@media(max-width:850px){.page{padding:20px}.page-heading{align-items:stretch;flex-direction:column;gap:16px}.request-summary{grid-template-columns:1fr 1fr}.filters{grid-template-columns:1fr}.filters>input:first-child{grid-column:auto}.limit-range{grid-template-columns:1fr}}@media(max-width:520px){.request-summary{grid-template-columns:1fr}}
  `,
})
export class ApplicationList implements OnInit {
  private readonly cardApplications = signal<CardApplication[]>([]);
  private readonly supplementaryApplications = signal<SupplementaryApplication[]>([]);
  private readonly limitRequests = signal<LimitIncreaseRequest[]>([]);
  protected readonly error = signal('');
  protected readonly isManager = computed(() => this.auth.currentUser()?.role === 'Manager');
  protected readonly search = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly requestTypeFilter = signal('All');
  protected readonly minimumLimit = signal('');
  protected readonly maximumLimit = signal('');
  protected readonly sortOrder = signal<'Newest' | 'Oldest' | 'LimitDesc' | 'LimitAsc'>('Newest');
  protected readonly dateFilter = signal<'All' | 'Today' | 'Week'>('All');
  protected readonly requestTypeOptions = [
    { label: 'Tüm başvurular', value: 'All' }, { label: 'Kredi Kartı', value: 'CardApplication' },
    { label: 'Ek Kart', value: 'SupplementaryCard' }, { label: 'Limit Artırımı', value: 'LimitIncrease' },
    { label: 'Limit Azaltımı', value: 'LimitDecrease' },
  ];
  protected readonly statusOptions = [
    { label: 'Tüm durumlar', value: 'All' }, { label: 'Beklemede', value: 'Pending' },
    { label: 'Onaylandı', value: 'Approved' }, { label: 'Reddedildi', value: 'Rejected' },
    { label: 'Revizyonda', value: 'Revision' },
  ];

  private readonly allApplications = computed<UnifiedApplication[]>(() => {
    const customerIds = new Map(this.cardApplications().map(item => [item.customerNumber, item.customerId]));
    return [
      ...this.cardApplications().map(item => this.mapCardApplication(item)),
      ...this.supplementaryApplications().map(item => this.mapSupplementaryApplication(item)),
      ...this.limitRequests().map(item => this.mapLimitRequest(item, customerIds.get(item.customerNumber) ?? null)),
    ];
  });
  protected readonly filtersActive = computed(() => !!this.search().trim() || this.statusFilter() !== 'All' || this.requestTypeFilter() !== 'All' || !!this.minimumLimit() || !!this.maximumLimit() || this.sortOrder() !== 'Newest' || this.dateFilter() !== 'All');
  protected readonly filteredApplications = computed(() => {
    const query = this.search().trim().toLocaleLowerCase('tr-TR');
    const minimum = Number(this.minimumLimit()) || 0;
    const maximum = Number(this.maximumLimit()) || Number.MAX_SAFE_INTEGER;
    return this.allApplications().filter(item =>
      (this.statusFilter() === 'All' || item.status === this.statusFilter()) &&
      (this.requestTypeFilter() === 'All' || item.requestType === this.requestTypeFilter()) &&
      item.requestedLimit >= minimum && item.requestedLimit <= maximum && this.isInSelectedPeriod(item.createdAtUtc) &&
      (!query || `${item.applicationNumber} ${item.customerFullName} ${item.productLabel} ${this.requestTypeLabel(item.requestType)}`.toLocaleLowerCase('tr-TR').includes(query)))
      .sort((left, right) => {
        if (this.sortOrder() === 'LimitDesc') return right.requestedLimit - left.requestedLimit;
        if (this.sortOrder() === 'LimitAsc') return left.requestedLimit - right.requestedLimit;
        const difference = new Date(right.createdAtUtc).getTime() - new Date(left.createdAtUtc).getTime();
        return this.sortOrder() === 'Newest' ? difference : -difference;
      });
  });

  constructor(private readonly applicationApi: CardApplicationApiService, private readonly platformApi: PlatformApiService, private readonly cardApi: CardApiService, private readonly auth: AuthService, route: ActivatedRoute) {
    const status = route.snapshot.queryParamMap.get('status');
    if (this.statusOptions.some(option => option.value === status)) this.statusFilter.set(status!);
    const type = route.snapshot.queryParamMap.get('type');
    if (this.requestTypeOptions.some(option => option.value === type)) this.requestTypeFilter.set(type!);
  }

  ngOnInit(): void { this.load(); }
  protected requestTypeCount(value: string): number { return this.allApplications().filter(item => item.requestType === value).length; }
  protected toggleRequestType(value: string): void { this.requestTypeFilter.set(this.requestTypeFilter() === value ? 'All' : value); }
  protected requestTypeLabel(value: RequestType): string { return ({ CardApplication: 'Kredi Kartı', SupplementaryCard: 'Ek Kart', LimitIncrease: 'Limit Artırımı', LimitDecrease: 'Limit Azaltımı' } as Record<RequestType, string>)[value]; }
  protected requestTypeClass(value: RequestType): string { return `request-type ${{ CardApplication: 'type-card', SupplementaryCard: 'type-supplementary', LimitIncrease: 'type-increase', LimitDecrease: 'type-decrease' }[value]}`; }
  protected statusLabel(value: string): string { return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda', Withdrawn: 'Geri Çekildi', Cancelled: 'İptal Edildi' } as Record<string, string>)[value] ?? value; }
  protected statusClass(value: string): string { return `status ${value.toLowerCase()}`; }
  protected formatDate(value: string): string { return new Date(value).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' }); }
  protected clearFilters(): void { this.search.set(''); this.statusFilter.set('All'); this.requestTypeFilter.set('All'); this.minimumLimit.set(''); this.maximumLimit.set(''); this.sortOrder.set('Newest'); this.dateFilter.set('All'); }

  private mapCardApplication(item: CardApplication): UnifiedApplication {
    return { key: `card-${item.id}`, id: item.id, applicationNumber: item.applicationNumber, requestType: 'CardApplication', customerId: item.customerId, customerFullName: item.customerFullName, productLabel: item.cardTypeName, requestedLimit: item.requestedLimit, createdAtUtc: item.createdAtUtc, status: item.status, detailLink: [this.isManager() ? '/manager/applications' : '/officer/applications', item.id], canRevise: !this.isManager() && item.status === 'Revision' };
  }
  private mapSupplementaryApplication(item: SupplementaryApplication): UnifiedApplication {
    return { key: `supplementary-${item.id}`, id: item.id, applicationNumber: item.applicationNumber, requestType: 'SupplementaryCard', customerId: item.primaryCustomerId, customerFullName: `${item.primaryCustomer} → ${item.holderCustomer}`, productLabel: `Ek kart · ${item.relationship}`, requestedLimit: item.requestedLimit, createdAtUtc: item.createdAtUtc, status: item.status, detailLink: [this.isManager() ? '/manager/supplementary-applications' : '/officer/supplementary-applications', item.id], canRevise: false };
  }
  private mapLimitRequest(item: LimitIncreaseRequest, customerId: number | null): UnifiedApplication {
    const requestType: RequestType = item.changeType === 'Decrease' ? 'LimitDecrease' : 'LimitIncrease';
    return { key: `limit-${item.cardId}-${item.requestedAtUtc}`, id: item.cardId, applicationNumber: `LMT-${item.cardId.toString().padStart(6, '0')}`, requestType, customerId, customerFullName: item.customerFullName, productLabel: `${item.cardTypeName} · ${item.maskedCardNumber}`, requestedLimit: item.requestedNewLimit, createdAtUtc: item.requestedAtUtc, status: item.status, detailLink: this.isManager() ? ['/manager/limit-increases', item.cardId] : ['/officer/cards', item.cardId], canRevise: false };
  }
  private isInSelectedPeriod(value: string): boolean {
    if (this.dateFilter() === 'All') return true;
    const date = new Date(value); const today = new Date(); today.setHours(0, 0, 0, 0);
    if (this.dateFilter() === 'Today') return date >= today;
    const weekStart = new Date(today); const day = weekStart.getDay() || 7; weekStart.setDate(weekStart.getDate() - day + 1);
    return date >= weekStart;
  }
  private load(): void {
    const failed: string[] = [];
    const report = (label: string) => { failed.push(label); this.error.set(`${failed.join(', ')} kayıtları yüklenemedi; diğer kayıtlar gösteriliyor.`); };
    const cardSource = this.isManager() ? this.applicationApi.getAll() : this.applicationApi.getMine();
    cardSource.subscribe({ next: items => this.cardApplications.set(items), error: () => report('Kredi kartı') });
    this.platformApi.supplementary().subscribe({ next: items => this.supplementaryApplications.set(items), error: () => report('Ek kart') });
    this.cardApi.getLimitIncreases().subscribe({ next: items => this.limitRequests.set(items), error: () => report('Limit değişikliği') });
  }
}
