import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardApiService, RecentOfficerRequest } from '../../../core/services/dashboard-api.service';
import { CardApplicationApiService } from '../../applications/card-application-api.service';
import { CardApplication } from '../../applications/card-application.models';

@Component({
  selector: 'app-officer-dashboard',
  imports: [RouterLink],
  templateUrl: './officer-dashboard.html',
  styleUrl: './officer-dashboard.scss'
})
export class OfficerDashboard implements OnInit {
  protected readonly allApplications = signal<CardApplication[]>([]);
  protected readonly recentRequests = signal<RecentOfficerRequest[]>([]);
  protected readonly periodCards = computed(() => {
    const applications = this.allApplications();
    return [
      { label: 'Toplam Başvuru', value: applications.length, detail: 'Tüm kayıtlar', route: '/officer/applications', status: '' },
      { label: 'Bekleyen Başvuru', value: applications.filter(item => item.status === 'Pending').length, detail: 'Değerlendirme sırası', route: '/officer/applications', status: 'Pending' },
      { label: 'Revizyon Bekleyen', value: applications.filter(item => item.status === 'Revision').length, detail: 'İşlem bekliyor', route: '/officer/applications', status: 'Revision' }
    ];
  });
  protected readonly dashboardSearch = signal('');
  protected readonly dashboardStatus = signal('All');
  protected readonly dashboardType = signal('All');
  protected readonly dashboardSort = signal<'Newest' | 'Oldest'>('Newest');
  protected readonly dashboardDate = signal<'All' | 'Today' | 'Week'>('All');
  protected readonly filterOptions = [
    { label: 'Tümü', value: 'All' }, { label: 'Beklemede', value: 'Pending' },
    { label: 'Onaylandı', value: 'Approved' }, { label: 'Reddedildi', value: 'Rejected' },
    { label: 'Revizyonda', value: 'Revision' }
  ];
  protected readonly requestTypeOptions = [
    { label: 'Tüm işlem türleri', value: 'All' },
    { label: 'Kart Başvurusu', value: 'CardApplication' },
    { label: 'Ek Kart', value: 'SupplementaryCard' },
    { label: 'Limit Artırımı', value: 'LimitIncrease' },
    { label: 'Limit Azaltımı', value: 'LimitDecrease' },
  ];
  protected readonly dashboardFiltersActive = computed(() => !!this.dashboardSearch().trim()
    || this.dashboardType() !== 'All' || this.dashboardStatus() !== 'All'
    || this.dashboardSort() !== 'Newest' || this.dashboardDate() !== 'All');

  private readonly requestSource = computed<RecentOfficerRequest[]>(() => {
    const dashboardRequests = this.recentRequests();
    if (dashboardRequests.length) return dashboardRequests;

    return this.allApplications().map(item => ({
      requestType: 'CardApplication' as const,
      entityId: item.id,
      referenceNumber: item.applicationNumber,
      customerId: item.customerId,
      customerName: item.customerFullName,
      description: `${item.cardTypeName} kart başvurusu`,
      status: item.status,
      createdAtUtc: item.createdAtUtc
    }));
  });

  protected readonly applications = computed(() => {
    const query = this.dashboardSearch().trim().toLocaleLowerCase('tr-TR');
    return this.requestSource()
      .filter(item => (this.dashboardStatus() === 'All' || item.status === this.dashboardStatus())
        && (this.dashboardType() === 'All' || item.requestType === this.dashboardType())
        && this.isInSelectedPeriod(item.createdAtUtc)
        && (!query || `${item.referenceNumber} ${item.customerName} ${item.description}`.toLocaleLowerCase('tr-TR').includes(query)))
      .sort((left, right) => (new Date(right.createdAtUtc).getTime() - new Date(left.createdAtUtc).getTime())
        * (this.dashboardSort() === 'Newest' ? 1 : -1))
      .slice(0, 8)
      .map(item => ({
        id: item.entityId, number: item.referenceNumber, customer: item.customerName,
        type: this.requestTypeLabel(item.requestType), typeClass: item.requestType.toLowerCase(),
        description: item.description, date: new Date(item.createdAtUtc).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' }),
        status: this.statusLabel(item.status), className: item.status.toLowerCase(), route: this.requestRoute(item),
        customerRoute: ['/officer/customers', String(item.customerId)]
      }));
  });

  protected readonly cardTypeDistribution = computed(() => {
    const total = this.allApplications().length;
    return ['Gold', 'Platinum', 'Classic', 'Platinum Plus'].map((name, index) => {
      const count = this.allApplications().filter(item => this.cardTier(item.cardTypeName) === name).length;
      return { name, count, percentage: total ? Math.round(count * 100 / total) : 0,
        className: name.toLowerCase(), color: ['#d0a53c', '#98a5b3', '#2d5d8e', '#0b2a4d'][index] };
    });
  });
  protected readonly selectedCardTypeName = signal('');
  protected readonly selectedCardType = computed(() =>
    this.cardTypeDistribution().find(item => item.name === this.selectedCardTypeName()) ?? null);
  protected readonly donutBackground = computed(() => {
    if (!this.allApplications().length) {
      return 'radial-gradient(circle at center, #fff 0 47%, transparent 49%), conic-gradient(#edf0f3 0 100%)';
    }
    let start = 0;
    const segments = this.cardTypeDistribution().map((item, index, items) => {
      const end = index === items.length - 1 ? 100 : start + item.percentage;
      const segment = `${item.color} ${start}% ${end}%`;
      start = end;
      return segment;
    });
    return `radial-gradient(circle at center, #fff 0 47%, transparent 49%), conic-gradient(${segments.join(',')})`;
  });
  protected readonly statusDistribution = computed(() => {
    const total = this.allApplications().length;
    return [
      { status: 'Approved', label: 'Onaylandı', className: 'approved-bar' },
      { status: 'Rejected', label: 'Reddedildi', className: 'rejected-bar' },
      { status: 'Revision', label: 'Revizyonda', className: 'revision-bar' },
      { status: 'Pending', label: 'Beklemede', className: 'pending-bar' }
    ].map(item => ({ ...item, percentage: total ? Math.round(this.allApplications()
      .filter(application => application.status === item.status).length * 100 / total) : 0 }));
  });

  constructor(
    protected readonly authService: AuthService,
    private readonly dashboardApi: DashboardApiService,
    private readonly applicationApi: CardApplicationApiService
  ) {}

  ngOnInit(): void {
    this.dashboardApi.officer().subscribe({ next: data => {
      this.recentRequests.set(data.recentRequests ?? []);
    }});
    this.applicationApi.getMine().subscribe({ next: data => this.allApplications.set(data) });
  }

  protected selectCardTypeByPointer(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const angle = (Math.atan2(
      event.clientY - (rect.top + rect.height / 2),
      event.clientX - (rect.left + rect.width / 2)
    ) * 180 / Math.PI + 450) % 360;
    const percent = angle / 3.6;
    let total = 0;
    const selected = this.cardTypeDistribution().find(item => {
      total += item.percentage;
      return percent <= total;
    });
    this.selectedCardTypeName.set(selected?.name ?? '');
  }

  protected clearDashboardFilters(): void {
    this.dashboardSearch.set('');
    this.dashboardType.set('All');
    this.dashboardStatus.set('All');
    this.dashboardSort.set('Newest');
    this.dashboardDate.set('All');
  }

  private isInSelectedPeriod(value: string): boolean {
    if (this.dashboardDate() === 'All') return true;
    const date = new Date(value);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (this.dashboardDate() === 'Today') return date >= today;
    const weekStart = new Date(today);
    const day = weekStart.getDay() || 7;
    weekStart.setDate(weekStart.getDate() - day + 1);
    return date >= weekStart;
  }

  private statusLabel(status: string): string {
    return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda',
      Unknown: 'Bilinmiyor' } as Record<string, string>)[status] ?? status;
  }

  private requestTypeLabel(type: RecentOfficerRequest['requestType']): string {
    return ({ CardApplication: 'Kart Başvurusu', SupplementaryCard: 'Ek Kart',
      LimitIncrease: 'Limit Artırımı', LimitDecrease: 'Limit Azaltımı' } as Record<string, string>)[type] ?? type;
  }

  private requestRoute(item: RecentOfficerRequest): string[] {
    if (item.requestType === 'CardApplication') return ['/officer/applications', String(item.entityId)];
    if (item.requestType === 'SupplementaryCard') return ['/officer/supplementary-applications', String(item.entityId)];
    return ['/officer/cards', String(item.entityId)];
  }

  private cardTier(cardTypeName: string): string {
    const normalized = cardTypeName.trim().toLocaleLowerCase('tr-TR');
    if (normalized.startsWith('platinum plus')) return 'Platinum Plus';
    if (normalized.startsWith('platinum')) return 'Platinum';
    if (normalized.startsWith('gold')) return 'Gold';
    return 'Classic';
  }
}
