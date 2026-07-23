import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardApiService } from '../../../core/services/dashboard-api.service';
import { CardApplicationApiService } from '../../applications/card-application-api.service';
import { CardApplication } from '../../applications/card-application.models';

@Component({
  selector: 'app-officer-dashboard',
  imports: [RouterLink],
  templateUrl: './officer-dashboard.html',
  styleUrl: './officer-dashboard.scss'
})
export class OfficerDashboard implements OnInit {
  protected readonly periodCards = [
    { label: 'Toplam Başvuru', value: 0, detail: 'Tüm kayıtlar' },
    { label: 'Bekleyen Başvuru', value: 0, detail: 'Değerlendirme sırası' },
    { label: 'Revizyon Bekleyen', value: 0, detail: 'İşlem bekliyor' }
  ];
  protected readonly statusCards = [
    { label: 'Bekleyen', value: 0 }, { label: 'Onaylanan', value: 0 },
    { label: 'Reddedilen', value: 0 }, { label: 'Revizyon', value: 0 }
  ];
  protected readonly allApplications = signal<CardApplication[]>([]);
  protected readonly dashboardSearch = signal('');
  protected readonly dashboardStatus = signal('All');
  protected readonly filterOptions = [
    { label: 'Tümü', value: 'All' }, { label: 'Beklemede', value: 'Pending' },
    { label: 'Onaylandı', value: 'Approved' }, { label: 'Reddedildi', value: 'Rejected' },
    { label: 'Revizyonda', value: 'Revision' }
  ];

  protected readonly applications = computed(() => {
    const query = this.dashboardSearch().trim().toLocaleLowerCase('tr-TR');
    return this.allApplications()
      .filter(item => (this.dashboardStatus() === 'All' || item.status === this.dashboardStatus()) &&
        (!query || `${item.applicationNumber} ${item.customerFullName} ${item.cardTypeName}`.toLocaleLowerCase('tr-TR').includes(query)))
      .slice(0, 5)
      .map(item => ({
        id: item.id, number: item.applicationNumber, customer: item.customerFullName,
        type: item.cardTypeName, date: new Date(item.createdAtUtc).toLocaleDateString('tr-TR'),
        status: this.statusLabel(item.status), className: item.status.toLowerCase()
      }));
  });

  protected readonly cardTypeDistribution = computed(() => {
    const total = this.allApplications().length;
    return ['Gold', 'Platinum', 'Classic', 'Premium'].map((name, index) => {
      const count = this.allApplications().filter(item => item.cardTypeName === name).length;
      return { name, count, percentage: total ? Math.round(count * 100 / total) : 0,
        className: name.toLowerCase(), color: ['#d0a53c', '#98a5b3', '#2d5d8e', '#0b2a4d'][index] };
    });
  });
  protected readonly primaryCardType = computed(() => [...this.cardTypeDistribution()].sort((a, b) => b.count - a.count)[0]);
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
      this.periodCards[0].value = data.totalApplications;
      this.periodCards[1].value = data.pendingApplications;
      this.periodCards[2].value = data.revisionApplications;
      this.statusCards[0].value = data.pendingApplications;
      this.statusCards[1].value = data.approvedApplications;
      this.statusCards[2].value = data.rejectedApplications;
      this.statusCards[3].value = data.revisionApplications;
    }});
    this.applicationApi.getMine().subscribe({ next: data => this.allApplications.set(data) });
  }

  private statusLabel(status: string): string {
    return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda' } as Record<string, string>)[status] ?? status;
  }
}
