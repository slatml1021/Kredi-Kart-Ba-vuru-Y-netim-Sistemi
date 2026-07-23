import { Component, OnInit, computed, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CardApplication, CardType } from '../card-application.models';
import { CardApplicationApiService } from '../card-application-api.service';

@Component({
  selector: 'app-application-list',
  imports: [ReactiveFormsModule, DecimalPipe, RouterLink],
  template: `
    <section class="page"><p class="eyebrow">BAŞVURU YÖNETİMİ</p><h1>Başvurularım</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <div class="filters"><input type="search" placeholder="Başvuru no, müşteri veya kart tipi ara" (input)="search.set($any($event.target).value)"><div>@for (item of filterOptions; track item.value) { <button type="button" [class.active]="statusFilter() === item.value" (click)="statusFilter.set(item.value)">{{ item.label }}</button> }</div></div>
    <div class="card"><table><thead><tr><th>Başvuru No</th><th>Müşteri</th><th>Kart</th><th>Talep</th><th>Durum</th><th></th></tr></thead><tbody>
    @for (application of filteredApplications(); track application.id) { <tr><td><a [routerLink]="['/officer/applications', application.id]">{{ application.applicationNumber }}</a></td><td>{{ application.customerFullName }}</td><td>{{ application.cardTypeName }}</td><td>{{ application.requestedLimit | number:'1.2-2' }} TL</td><td><span [class]="status(application.status)">{{ application.status }}</span></td><td>@if (application.status === 'Revision') { <button (click)="select(application)">Revize et</button> }</td></tr> }
    @empty { <tr><td colspan="6" class="empty">Filtreye uygun başvuru bulunmuyor.</td></tr> }</tbody></table></div>
    @if (selected(); as application) { <div class="overlay" role="presentation"><form class="modal" role="dialog" aria-modal="true" aria-labelledby="revision-title" [formGroup]="form" (ngSubmit)="resubmit()"><p class="eyebrow">BAŞVURU REVİZYONU</p><h2 id="revision-title">{{ application.applicationNumber }}</h2><span>Müdürün revizyona gönderdiği başvuruyu düzenleyip tekrar değerlendirmeye gönderin.</span><label>Kart tipi<select formControlName="cardTypeId">@for (type of cardTypes(); track type.id) { <option [value]="type.id">{{ type.name }}</option> }</select></label><label>Talep edilen limit<input type="number" formControlName="requestedLimit"></label><div class="modal-actions"><button type="button" class="secondary" (click)="selected.set(null)">Vazgeç</button><button type="submit">Tekrar gönder</button></div></form></div> }
    </section>`,
  styles: `.page{padding:2rem}.eyebrow{color:#b88918;font-weight:700;font-size:.75rem;letter-spacing:.08em}h1{color:#102a43}.filters{display:flex;justify-content:space-between;gap:1rem;align-items:center;margin:1.25rem 0}.filters input{min-width:320px}.filters button{background:#e9eef3;color:#486581}.filters button.active{background:#102a43;color:#fff}.card{background:#fff;border:1px solid #d9e2ec;border-radius:10px;padding:1.25rem;margin-top:1rem}table{width:100%;border-collapse:collapse}th,td{padding:.8rem;border-bottom:1px solid #edf2f7;text-align:left}th{color:#486581;font-size:.8rem}label{display:grid;gap:.4rem;font-weight:600}input,select{padding:.65rem;border:1px solid #bcccdc;border-radius:5px}.status{padding:.25rem .55rem;border-radius:999px;font-size:.78rem;font-weight:700}.pending{background:#e6f0ff;color:#1e5aa8}.revision{background:#fff3cd;color:#805b00}.approved{background:#d9f7e8;color:#18794e}.rejected{background:#ffe3e3;color:#b42318}button{background:#b88918;color:#fff;border:0;border-radius:5px;padding:.6rem .85rem;margin-right:.5rem;cursor:pointer}.secondary{background:#e9eef3;color:#243b53}.error{color:#b42318}.empty{text-align:center;color:#627d98}.overlay{position:fixed;inset:0;background:#102a43a6;display:grid;place-items:center;padding:1rem;z-index:50}.modal{width:min(560px,100%);background:#fff;border-radius:12px;box-shadow:0 24px 60px #102a4366;padding:1.5rem;display:grid;gap:1rem}.modal h2{margin:0;color:#102a43}.modal>span{color:#627d98}.modal-actions{display:flex;justify-content:flex-end;margin-top:.5rem}`
})
export class ApplicationList implements OnInit {
  protected readonly applications = signal<CardApplication[]>([]);
  protected readonly cardTypes = signal<CardType[]>([]);
  protected readonly selected = signal<CardApplication | null>(null);
  protected readonly error = signal('');
  protected readonly search = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly filterOptions = [{ label: 'Tümü', value: 'All' }, { label: 'Beklemede', value: 'Pending' }, { label: 'Onaylandı', value: 'Approved' }, { label: 'Reddedildi', value: 'Rejected' }, { label: 'Revizyonda', value: 'Revision' }];
  protected readonly filteredApplications = computed(() => {
    const query = this.search().trim().toLocaleLowerCase('tr-TR');
    return this.applications().filter(item => (this.statusFilter() === 'All' || item.status === this.statusFilter()) && (!query || `${item.applicationNumber} ${item.customerFullName} ${item.cardTypeName}`.toLocaleLowerCase('tr-TR').includes(query)));
  });
  protected readonly form = new FormGroup({ cardTypeId: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }), requestedLimit: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }) });
  constructor(private readonly api: CardApplicationApiService) {}
  ngOnInit(): void { this.load(); this.api.getCardTypes().subscribe({ next: types => this.cardTypes.set(types) }); }
  protected select(application: CardApplication): void { this.selected.set(application); this.form.setValue({ cardTypeId: application.cardTypeId, requestedLimit: application.requestedLimit }); }
  protected resubmit(): void { const application = this.selected(); if (!application || this.form.invalid) return; this.api.resubmit(application.id, this.form.getRawValue()).subscribe({ next: () => { this.selected.set(null); this.load(); }, error: response => this.error.set(response.error?.detail ?? 'Başvuru yeniden gönderilemedi.') }); }
  protected status(value: string): string { return `status ${value.toLowerCase()}`; }
  private load(): void { this.api.getMine().subscribe({ next: applications => this.applications.set(applications), error: () => this.error.set('Başvurular yüklenemedi.') }); }
}
