import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApiService, ManagerDashboard as ManagerDashboardData } from '../../../core/services/dashboard-api.service';
import { CardApplicationApiService } from '../../applications/card-application-api.service';
import { CardApplication } from '../../applications/card-application.models';
import { PlatformApiService, SlaDashboard } from '../../../core/services/platform-api.service';

type Period = 'Today' | 'Week' | 'Month' | 'Year';

@Component({
  selector: 'app-manager-dashboard',
  imports: [RouterLink],
  template: `
    <section class="manager-page">
      <header><div><p>MÜDÜR PANELİ</p><h1>Yönetim Paneli</h1><span>Başvuru süreçlerini ve operasyonel performansı takip edin.</span></div><div class="analysis-controls"><div class="periods"><button [class.active]="period()==='Today'" (click)="setPeriod('Today')">Bugün</button><button [class.active]="period()==='Week'" (click)="setPeriod('Week')">Bu Hafta</button><button [class.active]="period()==='Month'" (click)="setPeriod('Month')">Aylık</button><button [class.active]="period()==='Year'" (click)="setPeriod('Year')">Yıllık</button></div>@if(period()==='Month'){<label>Ay<select [value]="selectedMonth()" (change)="setMonth(+$any($event.target).value)">@for(month of analysisMonths;track month.value){<option [value]="month.value">{{month.label}}</option>}</select></label>}@if(period()==='Month'||period()==='Year'){<label>Yıl<select [value]="selectedYear()" (change)="setYear(+$any($event.target).value)">@for(year of analysisYears;track year){<option [value]="year">{{year}}</option>}</select></label>}</div></header>
      <div class="section-title"><h2>Operasyon Özeti</h2><span>{{ periodLabel() }} için başvuru performansı</span></div>
      <div class="metrics">
        <article routerLink="/manager/applications" [queryParams]="{ status: 'Pending' }"><span>Bekleyen Başvurular</span><b>{{ dashboard().pendingApplications }}</b></article>
        <article routerLink="/manager/all-applications"><span>Sonuçlandırılan</span><b>{{ dashboard().processedApplications }}</b><small>{{ dashboard().approvedApplications }} Onay · {{ dashboard().rejectedApplications }} Red</small></article>
        <article routerLink="/manager/all-applications" [queryParams]="{ status: 'Approved' }"><span>Onay Oranı</span><b>%{{ dashboard().approvalRate }}</b></article>
        <article routerLink="/manager/all-applications" [queryParams]="{ status: 'Rejected' }"><span>Red Oranı</span><b>%{{ dashboard().rejectionRate }}</b></article>
        <article routerLink="/manager/all-applications" [queryParams]="{ status: 'Revision' }"><span>Revizyona Gönderilen</span><b>{{ dashboard().revisionApplications }}</b></article>
      </div>
      <div class="sla-metrics">
        <article><span>Ortalama Değerlendirme Süresi</span><b>{{ duration(sla().averageEvaluationMinutes) }}</b></article>
        <article><span>Bugün Geciken Başvuru</span><b>{{ sla().overdueToday }}</b></article>
        <article><span>En Uzun Bekleyen</span><b>{{ sla().longestWaitingHours }} saat</b></article>
        <article><span>Revizyondan Dönüş Oranı</span><b>%{{ sla().revisionReturnRate }}</b></article>
      </div>
      <div class="section-title"><h2>Başvuru Analizleri</h2><span>Kart tipi dağılımı ve başvuru eğilimleri</span></div>
      <div class="analytics">
        <article><div class="card-head"><div><h3>Kart Tiplerine Göre Dağılım</h3><span>Grafikte ilgili rengin üzerine gelerek oranı inceleyin</span></div></div><div class="distribution"><button type="button" class="donut" [style.background]="donutGradient()" (mousemove)="selectCardTypeByPointer($event)" (mouseleave)="selectedCardTypeName.set('')" aria-label="Kart tipi dağılımı"><b>{{ selectedCardType() ? '%' + selectedCardType()!.percentage : dashboard().totalApplications }}</b><small>{{ selectedCardType()?.cardType ?? 'Toplam Başvuru' }}</small></button><ul>@for (item of dashboard().cardTypeDistribution; track item.cardType; let index = $index) { <li [class.selected]="selectedCardType()?.cardType === item.cardType" (mouseenter)="selectedCardTypeName.set(item.cardType)" (mouseleave)="selectedCardTypeName.set('')"><i [class]="'color-'+index"></i><span>{{ item.cardType }}</span><b>%{{ item.percentage }}</b></li> } @empty { <li>Henüz veri yok</li> }</ul></div></article>
        <article><div class="card-head"><div><h3>Başvuru Eğilimi</h3><span>{{ periodLabel() }} içindeki değişim</span></div></div><div class="bars">@for (item of dashboard().applicationTrend; track $index) { <div><span [style.height.%]="barHeight(item.count)" title="{{ item.count }} başvuru"></span><small>{{ item.label }}</small></div> }</div></article>
      </div>
      <article class="performance clickable" routerLink="/manager/workflow" title="İş Dağılımı ve Onaylar ekranını aç"><div class="card-head"><div><h3>Memur Performansları</h3><span>Başvuru oluşturma, sonuçlanma ve iş yükü görünümü</span></div><a routerLink="/manager/workflow">İş Dağılımı ve Onayları Aç →</a></div><div class="table-wrap"><table><thead><tr><th>MEMUR</th><th>OLUŞTURULAN BAŞVURU</th><th>ONAY ORANI</th><th>ORTALAMA İŞLEM SÜRESİ</th></tr></thead><tbody>@for (officer of dashboard().officerPerformance; track officer.officerName) { <tr><td>{{ officer.officerName }}</td><td>{{ officer.createdApplications }}</td><td>%{{ officer.approvalRate }}</td><td>{{ officer.averageProcessingMinutes }} dk</td></tr> } @empty { <tr><td colspan="4">Bu dönem için performans verisi bulunmuyor.</td></tr> }</tbody></table></div></article>
      <article class="queue"><div class="card-head"><div><h3>Öncelikli Değerlendirme Kuyruğu</h3><span>En eski bekleyen başvurular</span></div><a routerLink="/manager/applications">Bekleyen Başvurulara Git →</a></div><div class="table-wrap"><table><thead><tr><th>BAŞVURU NO</th><th>MÜŞTERİ</th><th>KART TİPİ</th><th>TALEP LİMİTİ</th><th>BAŞVURU TARİHİ</th><th></th></tr></thead><tbody>@for (item of pendingApplications().slice(0,5); track item.id) { <tr><td>{{ item.applicationNumber }}</td><td>{{ item.customerFullName }}</td><td>{{ item.cardTypeName }}</td><td>{{ formatCurrency(item.requestedLimit) }}</td><td>{{ formatDate(item.createdAtUtc) }}</td><td><a [routerLink]="['/manager/applications', item.id]">İncele</a></td></tr> } @empty { <tr><td colspan="6">Bekleyen başvuru bulunmuyor.</td></tr> }</tbody></table></div></article>
      <article class="queue"><div class="card-head"><div><h3>En Uzun Süredir Bekleyen Başvurular</h3><span>SLA durumuna göre sıralı</span></div></div><div class="table-wrap"><table><thead><tr><th>BAŞVURU</th><th>MÜŞTERİ</th><th>BEKLEME</th><th>SLA</th><th></th></tr></thead><tbody>@for(item of sla().longestWaiting;track item.applicationId){<tr><td>{{item.applicationNumber}}</td><td>{{item.customer}}</td><td>{{item.waitingHours}} saat</td><td><b [class]="'sla-'+item.slaStatus.toLowerCase()">{{slaLabel(item.slaStatus)}}</b></td><td><a [routerLink]="['/manager/applications',item.applicationId]">İncele</a></td></tr>}@empty{<tr><td colspan="5">Bekleyen başvuru yok.</td></tr>}</tbody></table></div></article>
    </section>
  `,
  styles: `
    :host{display:block}.manager-page{max-width:1450px;margin:0 auto;padding:30px;color:#20354a}header{display:flex;justify-content:space-between;align-items:flex-end;padding:0 4px;margin-bottom:24px}header p{margin:0 0 7px;color:#a17c22;font-size:10px;font-weight:800;letter-spacing:.15em}header h1{margin:0;color:#0b2a4d;font-size:30px}header span{display:block;margin-top:6px;color:#718093}.analysis-controls{display:flex;align-items:flex-end;gap:10px;flex-wrap:wrap;justify-content:flex-end}.analysis-controls label{display:grid;gap:4px;color:#718093;font-size:9px;font-weight:800}.analysis-controls select{min-width:105px;padding:9px 30px 9px 10px;border:1px solid #d8e0e7;border-radius:7px;color:#20354a;background:#fff}.periods{display:flex;border:1px solid #d8e0e7;border-radius:7px;overflow:hidden}.periods button{padding:10px 13px;border:0;border-right:1px solid #d8e0e7;color:#607184;background:#fff;cursor:pointer}.periods button:last-child{border:0}.periods button.active{color:#fff;background:#0b2a4d}.section-title{margin:18px 0 10px}.section-title h2{margin:0;color:#20354a;font-size:16px}.section-title span{color:#8290a0;font-size:10px}.metrics{display:grid;grid-template-columns:repeat(5,1fr);gap:14px}.sla-metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-top:14px}.sla-metrics article{padding:15px;border-left:4px solid #d1a83f;border-radius:7px;background:#fff}.sla-metrics span{display:block;color:#718093;font-size:10px}.sla-metrics b{display:block;margin-top:7px;color:#0b2a4d;font-size:18px}.metrics article,.analytics article,.performance,.queue{padding:18px;border:1px solid #dce3e9;border-radius:9px;background:#fff}.metrics span,.metrics small{display:block;color:#718093;font-size:10px}.metrics b{display:block;margin:15px 0 6px;color:#0b2a4d;font-size:30px}.analytics{display:grid;grid-template-columns:1fr 1fr;gap:14px}.card-head{display:flex;justify-content:space-between;align-items:center;margin-bottom:14px}.card-head h3{margin:0;color:#1d344c;font-size:14px}.card-head span{color:#8290a0;font-size:9px}.distribution{display:grid;grid-template-columns:200px 1fr;align-items:center;gap:20px;min-height:230px}.donut{width:150px;height:150px;margin:auto;border:0;border-radius:50%;display:grid;place-content:center;text-align:center;position:relative;cursor:pointer}.donut:after{content:'';position:absolute;inset:28px;border-radius:50%;background:#fff}.donut b,.donut small{z-index:1}.donut b{font-size:24px;color:#163654}.donut small{color:#718093}.distribution ul{margin:0;padding:0;list-style:none}.distribution li{display:flex;align-items:center;gap:8px;padding:8px;cursor:pointer;border-radius:6px}.distribution li.selected{background:#eef3f7}.distribution li span{flex:1;color:#65778a}.distribution i{width:9px;height:9px;border-radius:3px;background:#2d5b89}.distribution .color-1{background:#d1a83f}.distribution .color-2{background:#8f9dac}.distribution .color-3{background:#102a43}.bars{height:230px;display:flex;align-items:flex-end;gap:18px;padding:20px;overflow-x:auto}.bars div{height:100%;min-width:18px;flex:1;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px}.bars span{display:block;width:100%;min-height:6px;border-radius:5px 5px 0 0;background:linear-gradient(#174d78,#0b2a4d)}.bars small{color:#718093}.performance,.queue{margin-top:14px;padding:0;overflow:hidden}.performance.clickable{cursor:pointer;transition:border-color .2s,box-shadow .2s}.performance.clickable:hover{border-color:#b58b28;box-shadow:0 8px 24px rgba(11,42,77,.08)}.performance .card-head,.queue .card-head{padding:16px 18px;margin:0;border-bottom:1px solid #e4e9ee}.card-head a{color:#0b3d6d;font-size:11px;font-weight:800;text-decoration:none}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:13px 18px;border-bottom:1px solid #edf0f3;text-align:left;white-space:nowrap;font-size:11px}th{color:#718093;background:#fafbfc;font-size:8px;letter-spacing:.08em}td a{color:#0b3d6d;font-weight:800;text-decoration:none}.sla-overdue{color:#b13d3d}.sla-attention{color:#a47811}.sla-normal{color:#18734d}@media(max-width:1050px){.metrics{grid-template-columns:repeat(3,1fr)}.analytics{grid-template-columns:1fr}.sla-metrics{grid-template-columns:1fr 1fr}}@media(max-width:700px){.manager-page{padding:20px}header{align-items:flex-start;flex-direction:column;gap:15px}.analysis-controls{justify-content:flex-start}.metrics{grid-template-columns:1fr 1fr}.distribution{grid-template-columns:1fr}.bars{gap:8px;padding:10px}}@media(max-width:450px){.metrics,.sla-metrics{grid-template-columns:1fr}}
  `,
})
export class ManagerDashboard implements OnInit {
  protected readonly period = signal<Period>('Week');
  protected readonly selectedMonth = signal(new Date().getMonth() + 1);
  protected readonly selectedYear = signal(new Date().getFullYear());
  protected readonly analysisMonths = [
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
  ].map((label, index) => ({ label, value: index + 1 }));
  protected readonly analysisYears = Array.from({ length: 5 }, (_, index) => new Date().getFullYear() - index);
  protected readonly dashboard = signal<ManagerDashboardData>({
    pendingApplications: 0, approvedToday: 0, rejectedToday: 0, revisionApplications: 0,
    processedApplications: 0, approvedApplications: 0, rejectedApplications: 0,
    approvalRate: 0, rejectionRate: 0, totalApplications: 0,
    cardTypeDistribution: [], applicationTrend: [], officerPerformance: [],
  });
  protected readonly pendingApplications = signal<CardApplication[]>([]);
  protected readonly sla = signal<SlaDashboard>({ averageEvaluationMinutes: 0, overdueToday: 0, longestWaitingHours: 0, revisionReturnRate: 0, longestWaiting: [] });
  protected readonly selectedCardTypeName = signal('');
  protected readonly selectedCardType = computed(() =>
    this.dashboard().cardTypeDistribution.find(item => item.cardType === this.selectedCardTypeName()) ?? null);
  protected readonly periodLabel = computed(() => {
    if (this.period() === 'Today') return 'Bugün';
    if (this.period() === 'Week') return 'Bu hafta';
    if (this.period() === 'Year') return `${this.selectedYear()} yılı`;
    return `${this.analysisMonths[this.selectedMonth() - 1].label} ${this.selectedYear()}`;
  });
  protected readonly maxTrend = computed(() => Math.max(1, ...this.dashboard().applicationTrend.map(x => x.count)));

  constructor(private readonly dashboardApi: DashboardApiService, private readonly applicationApi: CardApplicationApiService, private readonly platformApi: PlatformApiService) {}
  ngOnInit(): void { this.load(); this.applicationApi.getPending().subscribe({ next: data => this.pendingApplications.set(data) }); this.platformApi.sla().subscribe(data=>this.sla.set(data)); }
  protected setPeriod(period: Period): void { this.period.set(period); this.load(); }
  protected setMonth(month: number): void { this.selectedMonth.set(month); this.load(); }
  protected setYear(year: number): void { this.selectedYear.set(year); this.load(); }
  protected barHeight(value: number): number { return Math.max(4, value * 100 / this.maxTrend()); }
  protected distributionOffset(index: number): string { return `${this.dashboard().cardTypeDistribution[index]?.percentage ?? 0}%`; }
  protected selectNextCardType(): void {
    const items = this.dashboard().cardTypeDistribution;
    if (!items.length) return;
    const currentIndex = items.findIndex(item => item.cardType === this.selectedCardTypeName());
    this.selectedCardTypeName.set(items[(currentIndex + 1) % items.length].cardType);
  }
  protected donutGradient(): string {
    const colors=['#2d5b89','#d1a83f','#8f9dac','#102a43'];let start=0;
    const stops=this.dashboard().cardTypeDistribution.map((item,index)=>{const end=start+item.percentage;const value=`${colors[index%colors.length]} ${start}% ${end}%`;start=end;return value;});
    return `conic-gradient(${stops.length?stops.join(','):'#e5e9ed 0 100%'})`;
  }
  protected selectCardTypeByPointer(event: MouseEvent): void {
    const rect=(event.currentTarget as HTMLElement).getBoundingClientRect();
    let angle=(Math.atan2(event.clientY-(rect.top+rect.height/2),event.clientX-(rect.left+rect.width/2))*180/Math.PI+450)%360;
    const percent=angle/3.6;let total=0;
    const selected=this.dashboard().cardTypeDistribution.find(item=>{total+=item.percentage;return percent<=total;});
    this.selectedCardTypeName.set(selected?.cardType??'');
  }
  protected duration(minutes:number):string{const hours=Math.floor(minutes/60);const mins=Math.round(minutes%60);return `${hours} sa ${mins} dk`;}
  protected slaLabel(value:string):string{return ({Normal:'Normal',Attention:'Dikkat',Overdue:'Gecikmiş'} as Record<string,string>)[value]??value;}
  protected formatCurrency(value: number): string { return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(value); }
  protected formatDate(value: string): string { return new Date(value).toLocaleDateString('tr-TR'); }
  private load(): void { this.dashboardApi.manager(this.period(), this.selectedMonth(), this.selectedYear()).subscribe({ next: data => this.dashboard.set(data) }); }
}
