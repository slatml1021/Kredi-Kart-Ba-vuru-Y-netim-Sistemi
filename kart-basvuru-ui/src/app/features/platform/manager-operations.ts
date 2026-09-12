import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AuditItem, DecisionQuality, PlatformApiService } from '../../core/services/platform-api.service';

@Component({
  selector: 'app-manager-operations',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule],
  template: `
    <section class="page">
      <header><p>OPERASYON VE DENETİM</p><h1>Karar Kalitesi ve Denetim Merkezi</h1><span>Müdür kararlarını, SLA istisnalarını, yeniden atamaları ve kritik işlemleri tek ekrandan izleyin.</span></header>
      @if(error()){<div class="error">{{error()}}</div>}
      @if(quality();as item){
        <div class="metrics">
          <article><span>Toplam Karar</span><b>{{item.totalDecisions}}</b><small>Son {{item.periodDays}} gün</small></article>
          <article><span>Ortalama Süre</span><b>{{item.averageEvaluationMinutes | number:'1.0-0'}} dk</b><small>Başvurudan karara</small></article>
          <article><span>Onay / Red / Revizyon</span><b>%{{item.approvalRate}} / %{{item.rejectionRate}} / %{{item.revisionRate}}</b><small>Karar dağılımı</small></article>
          <article><span>Geciken Açık İş</span><b>{{item.overdueOpenCount}}</b><small>8 saati aşan</small></article>
          <article><span>Çift Kontrol</span><b>{{item.dualApprovalCount}}</b><small>İkinci müdür onayı</small></article>
          <article><span>Yeniden Atama</span><b>{{item.reassignmentCount}}</b><small>Gerekçeli iş değişimi</small></article>
        </div>
        <article class="panel"><div class="panel-head"><div><h2>Müdür Karar Özeti</h2><span>Bu tablo performans puanı değil; karar hacmi ve süreç tutarlılığı görünümüdür.</span></div></div>
          <div class="table"><div class="row head"><b>Müdür</b><b>Toplam</b><b>Onay</b><b>Red</b><b>Revizyon</b><b>Ort. Süre</b></div>@for(manager of item.managers;track manager.userId){<div class="row"><b>{{manager.manager}}</b><span>{{manager.total}}</span><span>{{manager.approved}} · %{{manager.approvalRate}}</span><span>{{manager.rejected}}</span><span>{{manager.revision}}</span><span>{{manager.averageMinutes | number:'1.0-0'}} dk</span></div>}@empty{<p class="empty">Seçilen dönemde karar bulunmuyor.</p>}</div>
        </article>
      }
      <article class="panel audit"><div class="panel-head"><div><h2>Denetim Kayıtları</h2><span>Kim, ne zaman, hangi kayıtta ve hangi IP üzerinden işlem yaptı?</span></div><div class="filters"><input [formControl]="search" placeholder="İşlem, kayıt veya açıklama ara"><select [formControl]="action"><option value="">Tüm işlemler</option><option value="ApplicationReassigned">Yeniden atama</option><option value="ApplicationDocumentVerified">Belge doğrulama</option><option value="ApplicationCancelled">Operasyonel iptal</option><option value="ApplicationWithdrawn">Geri çekme</option><option value="KycAssessmentViewed">KYC görüntüleme</option><option value="Logout">Güvenli çıkış</option></select><button type="button" (click)="loadAudit()">Uygula</button></div></div>
        <div class="table"><div class="row audit-row head"><b>Tarih</b><b>Kullanıcı</b><b>İşlem</b><b>Kayıt</b><b>Açıklama</b><b>IP</b></div>@for(log of logs();track log.id){<div class="row audit-row"><span>{{log.createdAtUtc | date:'dd.MM.yyyy HH:mm:ss'}}</span><b>{{log.user || 'Sistem'}}</b><span>{{actionLabel(log.action)}}</span><span>{{log.entityName}} #{{log.entityId || '—'}}</span><span>{{log.detail || '—'}}</span><span>{{log.ipAddress || '—'}}</span></div>}@empty{<p class="empty">Filtreye uygun denetim kaydı bulunamadı.</p>}</div>
      </article>
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1320px;margin:auto;padding:34px;color:#18344f}header p{margin:0 0 7px;color:#9a7418;font-size:10px;font-weight:900;letter-spacing:.18em}header h1{margin:0;color:#0b2a4d;font-size:35px}header span,.panel-head span{color:#718093}.metrics{margin:24px 0;display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.metrics article{min-height:105px;padding:18px;display:grid;align-content:center;gap:7px;border:1px solid #dce3e9;border-radius:10px;background:#fff}.metrics span{color:#718093;font-size:11px;font-weight:800}.metrics b{font-size:23px}.metrics small{color:#8b98a4}.panel{margin-top:18px;border:1px solid #dce3e9;border-radius:11px;background:#fff;overflow:hidden}.panel-head{padding:18px;display:flex;align-items:flex-end;justify-content:space-between;gap:18px;border-bottom:1px solid #dce3e9}.panel h2{margin:0 0 5px}.filters{display:flex;gap:8px}.filters input,.filters select{padding:9px;border:1px solid #cad5df;border-radius:7px}.filters button{padding:9px 13px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}.row{display:grid;grid-template-columns:2fr repeat(5,1fr);gap:10px;padding:12px 18px;border-bottom:1px solid #edf1f4;font-size:12px}.row:last-child{border:0}.row.head{color:#718093;background:#f6f8fa;font-size:10px;text-transform:uppercase}.audit-row{grid-template-columns:150px 150px 170px 170px minmax(260px,1fr) 120px}.empty{padding:20px;color:#718093}.error{margin-top:15px;padding:12px;color:#9e3030;background:#fff0f0}@media(max-width:950px){.metrics{grid-template-columns:repeat(2,1fr)}.panel-head{align-items:flex-start;flex-direction:column}.table{overflow:auto}.row{min-width:760px}.audit-row{min-width:1100px}}@media(max-width:600px){.page{padding:20px}.metrics{grid-template-columns:1fr}.filters{width:100%;flex-direction:column}}
  `,
})
export class ManagerOperations implements OnInit {
  protected readonly quality = signal<DecisionQuality | null>(null);
  protected readonly logs = signal<AuditItem[]>([]);
  protected readonly error = signal('');
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly action = new FormControl('', { nonNullable: true });
  constructor(private readonly api: PlatformApiService) {}
  ngOnInit(): void { this.api.decisionQuality().subscribe({next:value=>this.quality.set(value),error:()=>this.error.set('Karar kalitesi verileri yüklenemedi.')}); this.loadAudit(); }
  protected loadAudit(): void { this.api.audit(this.search.value.trim(),this.action.value).subscribe({next:value=>this.logs.set(value),error:()=>this.error.set('Denetim kayıtları yüklenemedi.')}); }
  protected actionLabel(value:string):string{return({ApplicationReassigned:'Yeniden atama',ApplicationAssigned:'İş atama',ApplicationAutoAssigned:'Otomatik atama',ApplicationDocumentVerified:'Belge doğrulama',ApplicationCancelled:'Operasyonel iptal',ApplicationWithdrawn:'Geri çekme',KycAssessmentViewed:'KYC kontrolü',Logout:'Güvenli çıkış'}as Record<string,string>)[value]??value;}
}
