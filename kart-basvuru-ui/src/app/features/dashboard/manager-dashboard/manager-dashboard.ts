import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApiService, ManagerDashboard as ManagerDashboardData } from '../../../core/services/dashboard-api.service';
import { CardApplicationApiService } from '../../applications/card-application-api.service';
import { CardApplication } from '../../applications/card-application.models';

@Component({
  selector: 'app-manager-dashboard',
  imports: [RouterLink],
  template: `
    <section class="manager-page">
      <header><div><p>YÖNETİCİ PANELİ</p><h1>Değerlendirme Merkezi</h1><span>Başvuru kuyruğunu ve günlük karar performansını tek ekrandan yönetin.</span></div><a routerLink="/manager/applications">Tüm başvurular →</a></header>
      <div class="metrics">
        <article><span>Değerlendirme Bekleyen</span><b>{{ dashboard().pendingApplications }}</b><small>İşlem sırasındaki başvurular</small></article>
        <article><span>Bugün Onaylanan</span><b>{{ dashboard().approvedToday }}</b><small>Bugünkü olumlu kararlar</small></article>
        <article><span>Bugün Reddedilen</span><b>{{ dashboard().rejectedToday }}</b><small>Bugünkü olumsuz kararlar</small></article>
        <article><span>Revizyon Bekleyen</span><b>{{ dashboard().revisionApplications }}</b><small>Memur işlemi bekleniyor</small></article>
      </div>
      <article class="queue">
        <div class="queue-title"><div><h2>Öncelikli Değerlendirme Kuyruğu</h2><p>En eski başvurudan başlayarak güncel bekleyen kayıtlar</p></div><span>{{ pendingApplications().length }} kayıt</span></div>
        <div class="table-wrap"><table><thead><tr><th>BAŞVURU NO</th><th>MÜŞTERİ</th><th>KART TİPİ</th><th>TALEP LİMİTİ</th><th>OLUŞTURMA</th><th></th></tr></thead><tbody>
          @for (item of pendingApplications().slice(0, 8); track item.id) {
            <tr><td><b>{{ item.applicationNumber }}</b></td><td>{{ item.customerFullName }}</td><td>{{ item.cardTypeName }}</td><td>{{ formatCurrency(item.requestedLimit) }}</td><td>{{ formatDate(item.createdAtUtc) }}</td><td><a [routerLink]="['/manager/applications', item.id]">İncele →</a></td></tr>
          } @empty { <tr><td colspan="6" class="empty">Değerlendirme bekleyen başvuru bulunmuyor.</td></tr> }
        </tbody></table></div>
      </article>
    </section>
  `,
  styles: [`
    :host{display:block}.manager-page{display:grid;gap:24px}header{display:flex;justify-content:space-between;align-items:center;padding:28px;border-radius:20px;background:linear-gradient(120deg,#0b2a4d,#174d78);color:#fff}header p{margin:0 0 6px;font-size:12px;letter-spacing:1.6px;color:#a9c9e7}header h1{margin:0 0 8px;font-size:30px}header span{color:#d8e8f6}header a,.queue a{color:#fff;text-decoration:none;font-weight:700;background:#d0a53c;padding:11px 16px;border-radius:10px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}.metrics article,.queue{background:#fff;border:1px solid #e3e8ee;border-radius:16px;padding:20px;box-shadow:0 8px 24px rgba(11,42,77,.06)}.metrics span,.metrics small{display:block;color:#667789}.metrics b{display:block;font-size:34px;color:#0b2a4d;margin:10px 0}.queue-title{display:flex;justify-content:space-between;align-items:center;margin-bottom:18px}.queue-title h2{margin:0 0 4px;color:#0b2a4d}.queue-title p{margin:0;color:#6d7d8d}.queue-title>span{background:#edf4fa;color:#174d78;padding:7px 12px;border-radius:999px;font-weight:700}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:14px;border-bottom:1px solid #edf0f3;white-space:nowrap}th{font-size:11px;color:#718093;letter-spacing:.5px}.queue td a{display:inline-block;padding:7px 10px;font-size:13px}.empty{text-align:center;color:#718093;padding:28px}@media(max-width:900px){.metrics{grid-template-columns:repeat(2,1fr)}header{align-items:flex-start;gap:18px;flex-direction:column}}@media(max-width:560px){.metrics{grid-template-columns:1fr}}
  `]
})
export class ManagerDashboard implements OnInit {
  protected readonly dashboard = signal<ManagerDashboardData>({ pendingApplications: 0, approvedToday: 0, rejectedToday: 0, revisionApplications: 0 });
  protected readonly pendingApplications = signal<CardApplication[]>([]);

  constructor(private readonly dashboardApi: DashboardApiService, private readonly applicationApi: CardApplicationApiService) {}

  ngOnInit(): void {
    this.dashboardApi.manager().subscribe({ next: data => this.dashboard.set(data) });
    this.applicationApi.getPending().subscribe({ next: data => this.pendingApplications.set(data) });
  }

  protected formatCurrency(value: number): string {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(value);
  }

  protected formatDate(value: string): string {
    return new Date(value).toLocaleDateString('tr-TR');
  }
}
