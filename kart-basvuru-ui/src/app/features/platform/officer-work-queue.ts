import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlatformApiService, WorkflowApplication } from '../../core/services/platform-api.service';

@Component({
  selector: 'app-officer-work-queue',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `
    <section class="page">
      <header class="page-title"><div><small>OPERASYON MERKEZİ</small><h1>İşlerim</h1><p>Size atanmış açık başvuruları öncelik ve bekleme süresine göre yönetin.</p></div><button (click)="load()">Yenile</button></header>
      @if (error()) { <div class="alert">{{ error() }}</div> }
      @if (message()) { <div class="success">{{ message() }}</div> }
      <aside class="queue-info"><b>İşlerim neyi gösterir?</b><span>Bu alan size atanmış işleri ve henüz sorumlusu olmayan açık havuz kayıtlarını gösterir. Atanmamış bir kaydı “Üstlen” ile kendi kuyruğunuza alabilirsiniz. Sıralama kritik risk, SLA gecikmesi ve bekleme süresine göre otomatik yapılır.</span></aside>
      <div class="metrics"><article><span>Açık İş</span><b>{{ items().length }}</b></article><article class="danger"><span>Kritik</span><b>{{ critical() }}</b></article><article class="warn"><span>Revizyonda</span><b>{{ revisions() }}</b></article><article><span>Geciken</span><b>{{ overdue() }}</b></article></div>
      <div class="panel">
        <div class="panel-head"><div><h2>Öncelikli İş Kuyruğu</h2><p>Kritik işler ve SLA süresi aşan kayıtlar listenin başında gösterilir.</p></div><label>Filtre<select [value]="filter()" (change)="setFilter($event)"><option value="all">Tümü</option><option value="Kritik">Kritik</option><option value="Yüksek">Yüksek</option><option value="Normal">Normal</option></select></label></div>
        <div class="table-wrap"><table><thead><tr><th>Öncelik</th><th>Başvuru</th><th>Müşteri</th><th>Kart / Limit</th><th>Aşama</th><th>SLA</th><th></th></tr></thead><tbody>
          @for (item of filtered(); track item.id) { <tr><td><span class="badge" [class]="'badge '+item.priority.toLowerCase()">{{ item.priority }}</span></td><td><b>{{ item.applicationNumber }}</b><small>{{ item.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</small></td><td><b>{{ item.customer }}</b><small>Risk: {{ risk(item.riskLevel) }} · {{ item.score }}/100</small></td><td>{{ item.cardType }}<small>{{ item.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0':'tr-TR' }}</small></td><td>{{ stage(item.workflowStage) }}</td><td><b [class.sla-danger]="item.slaStatus === 'Gecikmiş'">{{ item.slaStatus }}</b><small>{{ item.waitingHours }} saat</small></td><td>@if(item.assignedOfficerUserId){<a [routerLink]="['/officer/applications', item.id]">İncele →</a>}@else{<button type="button" class="claim" (click)="claim(item)" [disabled]="busyId()===item.id">{{ busyId()===item.id?'Alınıyor…':'Üstlen' }}</button>}</td></tr> }
          @empty { <tr><td colspan="7" class="empty">Bu filtrede açık iş bulunmuyor.</td></tr> }
        </tbody></table></div>
      </div>
    </section>`,
  styles: [`
    :host{display:block;color:#0b2849}.page{max-width:1500px;margin:auto;padding:36px}.page-title{display:flex;justify-content:space-between;gap:20px;align-items:end;margin-bottom:26px}.page-title small{color:#a97b13;font-weight:800;letter-spacing:.2em}.page-title h1{font-size:42px;margin:8px 0}.page-title p,.panel p{margin:0;color:#758399}.page-title button,.panel select{border:1px solid #ccd6e2;background:#fff;border-radius:10px;padding:12px 16px;font-weight:700}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:16px;margin-bottom:20px}.metrics article{background:#fff;border:1px solid #dce3eb;border-radius:14px;padding:20px;box-shadow:0 4px 16px #0b284908}.metrics span{display:block;color:#738198;font-weight:700}.metrics b{font-size:32px;display:block;margin-top:10px}.metrics .danger{border-top:4px solid #c33f45}.metrics .warn{border-top:4px solid #d59d1b}.panel{background:#fff;border:1px solid #dce3eb;border-radius:16px;overflow:hidden}.panel-head{padding:22px 24px;display:flex;justify-content:space-between;align-items:end;border-bottom:1px solid #e7ecf2}.panel h2{margin:0 0 5px}.panel label{display:flex;gap:10px;align-items:center;font-weight:700}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:1050px}th{background:#f6f8fb;color:#6c7a8e;text-align:left;font-size:12px;letter-spacing:.08em;padding:14px 18px}td{padding:17px 18px;border-top:1px solid #edf0f4}td small{display:block;color:#7d899b;margin-top:5px}td a{font-weight:800;color:#0b3d71;text-decoration:none}.badge{display:inline-block;padding:6px 10px;border-radius:999px;font-weight:800}.badge.kritik{background:#fde8ea;color:#a92830}.badge.yüksek{background:#fff2d4;color:#926813}.badge.normal{background:#e7f4eb;color:#19703c}.sla-danger{color:#b52f37}.empty{text-align:center;color:#7d899b;padding:50px}.alert{padding:14px;background:#fde8ea;color:#9e2830;border-radius:10px;margin-bottom:16px}@media(max-width:900px){.page{padding:22px}.metrics{grid-template-columns:repeat(2,1fr)}.page-title{align-items:start}.page-title h1{font-size:34px}}
    .queue-info{display:flex;gap:12px;margin-bottom:18px;padding:14px 17px;border-left:4px solid #b88b25;border-radius:9px;color:#52677c;background:#fff8e8}.queue-info b{white-space:nowrap;color:#795b15}.success{margin-bottom:14px;padding:12px;border-radius:8px;color:#176c3b;background:#e7f4eb}.claim{padding:8px 12px;border:0;border-radius:7px;color:#fff;background:#174f7b;font-weight:800;cursor:pointer}.claim:disabled{opacity:.55}
  `],
})
export class OfficerWorkQueue {
  protected readonly items = signal<WorkflowApplication[]>([]); protected readonly error = signal(''); protected readonly message = signal(''); protected readonly filter = signal('all'); protected readonly busyId = signal<number | null>(null);
  protected readonly critical = computed(() => this.items().filter(x => x.priority === 'Kritik').length);
  protected readonly revisions = computed(() => this.items().filter(x => x.status === 'Revision').length);
  protected readonly overdue = computed(() => this.items().filter(x => x.slaStatus === 'Gecikmiş').length);
  protected readonly filtered = computed(() => this.filter() === 'all' ? this.items() : this.items().filter(x => x.priority === this.filter()));
  constructor(private readonly api: PlatformApiService) { this.load(); }
  protected load(){ this.error.set(''); this.api.myWork().subscribe({next:x=>this.items.set(x),error:()=>this.error.set('İş kuyruğu yüklenemedi.')}); }
  protected claim(item:WorkflowApplication){this.busyId.set(item.id);this.error.set('');this.message.set('');this.api.claimApplication(item.id).subscribe({next:()=>{this.busyId.set(null);this.message.set(`${item.applicationNumber} numaralı iş kuyruğunuza alındı.`);this.load()},error:response=>{this.busyId.set(null);this.error.set(response.error?.detail??'İş üstlenilemedi.')}})}
  protected setFilter(e:Event){this.filter.set((e.target as HTMLSelectElement).value)}
  protected stage(v:string){return v==='OfficerRevision'?'Memur revizyonu':v==='SecondManagerApproval'?'İkinci müdür onayı':'Müdür incelemesi'}
  protected risk(v:string){return ({LOW:'Düşük',MEDIUM:'Orta',HIGH:'Yüksek'} as Record<string,string>)[v]??v}
}
