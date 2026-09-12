import { CurrencyPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { PlatformApiService, SimulationResult } from '../../core/services/platform-api.service';
import { CustomerApiService } from '../customers/customer-api.service';
import { Customer } from '../customers/customer.models';
import { CardApplicationApiService } from '../applications/card-application-api.service';
import { CardType } from '../applications/card-application.models';

@Component({
  selector: 'app-simulation',
  imports: [ReactiveFormsModule, CurrencyPipe],
  styleUrl: './simulation-layout.scss',
  template: `
    <section class="page">
      <div class="heading">
        <div><p>KAYIT OLUŞTURMAZ</p><h1>Başvuru Simülasyonu</h1><span>Başvuru geçmişine yazmadan uygunluk, risk ve sistem önerilerini inceleyin.</span></div>
        <button type="button" class="secondary" (click)="clear()">Temizle</button>
      </div>

      <section class="customer-panel">
        <div class="section-title"><span>01</span><div><h2>Müşteri Seçimi</h2><p>TC Kimlik No, müşteri no veya ad soyad ile arayın.</p></div></div>
        <label class="search-field">Müşteri Ara
          <input [formControl]="customerQuery" placeholder="Yazdıkça eşleşen müşteriler listelenir" autocomplete="off">
        </label>
        @if (isSearching()) { <small class="search-state">Müşteriler aranıyor…</small> }
        @if (customerResults().length) {
          <div class="results">
            @for (customer of customerResults(); track customer.id) {
              <button type="button" (click)="selectCustomer(customer)">
                <span class="avatar">{{ customer.firstName.charAt(0) }}{{ customer.lastName.charAt(0) }}</span>
                <span><b>{{ customer.firstName }} {{ customer.lastName }}</b><small>{{ customer.customerNumber }} · {{ customer.nationalIdentityNumber }}</small></span>
                <em>{{ customer.isActive ? 'Seç →' : 'Pasif' }}</em>
              </button>
            }
          </div>
        }
        @if (selectedCustomer(); as customer) {
          <div class="selected-customer">
            <div><span>MÜŞTERİ</span><b>{{ customer.firstName }} {{ customer.lastName }}</b></div>
            <div><span>MÜŞTERİ NO</span><b>{{ customer.customerNumber }}</b></div>
            <div><span>TC KİMLİK NO</span><b>{{ customer.nationalIdentityNumber }}</b></div>
            <div><span>KULLANILABİLİR LİMİT</span><b>{{ customer.availableCardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
            <button type="button" (click)="clearCustomer()">Değiştir</button>
          </div>
        }
      </section>

      <form [formGroup]="form" (ngSubmit)="simulate()">
        <div class="section-title"><span>02</span><div><h2>Simülasyon Tercihleri</h2><p>Müşteri seçildikten sonra alanlar kullanıma açılır.</p></div></div>
        <div class="form-grid">
          <label>Kart Tipi
            <select formControlName="cardTypeId">@for(cardType of cardTypes();track cardType.id){<option [value]="cardType.id">{{ cardType.productName }} · {{ cardType.network }}</option>}</select>
          </label>
          <label>Talep Edilen Limit
            <input type="number" min="1" step="500" placeholder="Örn. 30.000" formControlName="requestedLimit">
            @if (form.controls.requestedLimit.touched && form.controls.requestedLimit.invalid) {
              <small class="validation">Talep edilen limit sıfırdan büyük olmalıdır.</small>
            }
          </label>
        </div>
        <button type="submit" [disabled]="form.invalid || !selectedCustomer() || isSimulating()">
          {{ isSimulating() ? 'Simülasyon Çalışıyor…' : 'Simülasyonu Çalıştır' }}
        </button>
      </form>

      <details class="algorithm-info">
        <summary>Risk puanı nasıl hesaplanır?</summary>
        <p>Başlangıç puanı 50’dir. Sistem karar vermez; yalnızca müdüre açıklanabilir karar desteği üretir.</p>
        <div><b>Gelir</b><span>20.000–39.999 TL: +6, 40.000 TL ve üzeri: +12, daha düşük gelir: −8.</span></div>
        <div><b>Çalışma durumu</b><span>Aktif çalışma ve meslek bilgisi: +10; diğer durumlar: −10.</span></div>
        <div><b>Yaş ve iletişim</b><span>23–65 yaş: +6; telefon ve e-posta birlikte doğrulanmışsa +8, değilse −8.</span></div>
        <div><b>Limit uyumu</b><span>Kapasitenin %70’ine kadar +12; üst banda yakın talep −5; kapasiteyi aşma derecesine göre −28, −42 veya −55.</span></div>
        <div><b>Diğer kontroller</b><span>Gelire göre yüksek Platinum segmenti −10, eksik müşteri profili −20.</span></div>
        <p><strong>75–100:</strong> düşük risk, <strong>50–74:</strong> orta risk, <strong>0–49:</strong> yüksek risk. Son karar her zaman yetkili müdüründür.</p>
      </details>

      @if (result(); as item) {
        <article class="result" [class.not-eligible]="!item.canCreateApplication">
          <div class="result-heading">
            <div><small>SİMÜLASYON SONUCU</small><h2>{{ item.canCreateApplication ? 'Başvuru oluşturulabilir' : 'Başvuru öncesi düzeltme gerekli' }}</h2></div>
            <b>{{ item.preAssessment.score }}/100 · {{ riskLabel(item.preAssessment.riskLevel) }}</b>
          </div>
          <div class="metrics">
            <div><span>ÖNERİLEN KART</span><b>{{ item.recommendedCard }}</b></div>
            <div><span>ÖNERİLEN LİMİT ARALIĞI</span><b>{{ item.suggestedMinimumLimit | currency:'TRY':'symbol-narrow':'1.0-0' }} – {{ item.suggestedMaximumLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
            <div><span>KARAR DESTEĞİ</span><b>{{ recommendationLabel(item.preAssessment.recommendation) }}</b></div>
          </div>
          <div class="analysis-grid">
            <section><h3>Risk Nedenleri</h3>
              @if (item.riskReasons.length) {
                <ul class="risks">@for (reason of item.riskReasons; track reason) { <li>{{ reason }}</li> }</ul>
              } @else { <p>Belirgin bir risk nedeni tespit edilmedi.</p> }
            </section>
            <section><h3>Sistem Önerileri</h3>
              <ul class="recommendations">@for (suggestion of item.systemRecommendations; track suggestion) { <li>{{ suggestion }}</li> }</ul>
            </section>
          </div>
          @if (item.missingFields.length) {
            <section class="missing"><h3>Eksik Bilgiler</h3><ul>@for (field of item.missingFields; track field) { <li>{{ field }}</li> }</ul></section>
          }
        </article>
      }

      @if (error()) { <div class="error" role="alert"><span>{{ error() }}</span><button type="button" aria-label="Uyarıyı kapat" (click)="error.set('')">×</button></div> }
      <div class="disclaimer">Bu simülasyon gerçek başvuru oluşturmaz ve başvuru geçmişine eklenmez.</div>
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:980px;margin:auto;padding:32px;color:#20364d}.heading{display:flex;justify-content:space-between;align-items:end}.heading p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.16em}h1{margin:7px 0 4px;color:#0b2a4d}.heading span{color:#718093}.secondary{color:#40566c!important;background:#e9eef3!important}.customer-panel,form,.result{margin-top:22px;padding:22px;border:1px solid #dce3e9;border-radius:11px;background:#fff}.section-title{display:flex;align-items:center;gap:10px;margin-bottom:15px}.section-title>span{width:34px;height:34px;display:grid;place-items:center;border-radius:50%;color:#916b12;background:#fff3d5;font-weight:900}.section-title h2,.section-title p{margin:0}.section-title p{margin-top:3px;color:#718093;font-size:11px}.search-field,label{display:grid;gap:6px;font-weight:700}input,select{padding:11px;border:1px solid #cad5df;border-radius:7px;font:inherit}input:disabled,select:disabled{color:#8b98a5;background:#edf1f4;cursor:not-allowed}.search-state{display:block;margin-top:7px;color:#718093}.results{display:grid;gap:6px;margin-top:10px}.results button{display:flex;align-items:center;gap:10px;margin:0;color:#20364d;border:1px solid #dce3e9;background:#fff;text-align:left}.results button>span:nth-child(2){display:grid;gap:3px;flex:1}.results small{color:#718093}.results em{color:#174d78;font-style:normal;font-weight:800}.avatar{width:36px;height:36px;display:grid;place-items:center;border-radius:50%;color:#fff;background:#123f6d}.selected-customer{position:relative;display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin-top:13px}.selected-customer div{display:grid;gap:5px;padding:12px;border:1px solid #e0e6eb;border-radius:7px;background:#f8fafb}.selected-customer span,.metrics span{color:#7b8997;font-size:9px;font-weight:800}.selected-customer b{font-size:11px}.selected-customer button{position:absolute;right:8px;bottom:-12px;margin:0;padding:6px 9px}.form-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:13px}button{padding:11px 14px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800;cursor:pointer}button:disabled{cursor:not-allowed;opacity:.5}form>button{width:100%;margin-top:16px}.algorithm-info{margin-top:14px;padding:16px 18px;border:1px solid #d9e2ea;border-radius:10px;background:#f9fbfd}.algorithm-info summary{cursor:pointer;color:#174d78;font-weight:900}.algorithm-info p{color:#617487;line-height:1.55}.algorithm-info div{display:grid;grid-template-columns:145px 1fr;gap:12px;padding:8px 0;border-top:1px solid #e6ebef}.algorithm-info span{color:#607386}.validation{color:#a52b2b}.result{border-top:4px solid #18815a}.result.not-eligible{border-top-color:#b88918}.result-heading{display:flex;justify-content:space-between;align-items:center}.result-heading small{color:#a17c22;font-weight:900}.result-heading h2{margin:5px 0 0}.result-heading>b{padding:11px 14px;border-radius:7px;color:#fff;background:#174d78}.metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:9px;margin:17px 0}.metrics div{display:grid;gap:6px;padding:13px;border-radius:7px;background:#f4f7fa}.analysis-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}.analysis-grid section,.missing{padding:14px;border:1px solid #e0e6eb;border-radius:8px}.analysis-grid h3,.missing h3{margin:0 0 10px}.analysis-grid ul,.missing ul{margin:0;padding-left:19px;line-height:1.7}.risks li::marker{color:#b54444}.recommendations li::marker{color:#18815a}.analysis-grid p{color:#718093}.missing{margin-top:12px;background:#fff9e9}.error,.disclaimer{margin-top:16px;padding:12px;border-radius:8px}.error{display:flex;align-items:center;justify-content:space-between;gap:12px;color:#a43a3a;background:#fff0f0}.error button{padding:0;color:inherit;background:transparent;font-size:20px}.disclaimer{border-left:4px solid #174d78;color:#52677c;background:#edf6ff;font-weight:700}@media(max-width:720px){.page{padding:20px}.heading,.result-heading{align-items:flex-start;flex-direction:column;gap:12px}.form-grid,.metrics,.analysis-grid,.selected-customer{grid-template-columns:1fr}.algorithm-info div{grid-template-columns:1fr;gap:3px}}
  `,
})
export class Simulation implements OnInit {
  protected readonly cardTypes = signal<CardType[]>([]);
  protected readonly result = signal<SimulationResult | null>(null);
  protected readonly error = signal('');
  protected readonly selectedCustomer = signal<Customer | null>(null);
  protected readonly customerResults = signal<Customer[]>([]);
  protected readonly isSearching = signal(false);
  protected readonly isSimulating = signal(false);
  protected readonly customerQuery = new FormControl('', { nonNullable: true });
  protected readonly form = new FormGroup({
    customerId: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    cardTypeId: new FormControl({ value: 1, disabled: true }, { nonNullable: true, validators: [Validators.min(1)] }),
    requestedLimit: new FormControl<number | null>({ value: null, disabled: true }, [Validators.required, Validators.min(1)]),
  });

  constructor(
    private readonly api: PlatformApiService,
    private readonly customerApi: CustomerApiService,
    private readonly applicationApi: CardApplicationApiService,
    private readonly route: ActivatedRoute,
    destroyRef: DestroyRef,
  ) {
    this.customerQuery.valueChanges.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(query => {
        const value = query.trim();
        this.result.set(null);
        this.error.set('');
        if (value.length < 2 || this.selectedCustomer()) {
          this.isSearching.set(false);
          return of<Customer[]>([]);
        }
        this.isSearching.set(true);
        return this.customerApi.search(value).pipe(catchError(() => {
          this.error.set('Müşteri sorgulanamadı.');
          return of<Customer[]>([]);
        }));
      }),
      takeUntilDestroyed(destroyRef),
    ).subscribe(customers => {
      this.isSearching.set(false);
      this.customerResults.set(customers);
    });
  }

  ngOnInit(): void {
    this.applicationApi.getCardTypes().subscribe({
      next: items => this.cardTypes.set(items),
      error: () => this.error.set('Kart ürün ve ağ kataloğu yüklenemedi.'),
    });
    const customerId = Number(this.route.snapshot.queryParamMap.get('customerId'));
    if (!Number.isInteger(customerId) || customerId <= 0) return;
    this.customerApi.getById(customerId).subscribe({
      next: customer => this.selectCustomer(customer),
      error: () => this.error.set('Simülasyon müşterisi bulunamadı.'),
    });
  }

  protected selectCustomer(customer: Customer): void {
    if (!customer.isActive) {
      this.error.set('Pasif müşteri için başvuru simülasyonu çalıştırılamaz.');
      return;
    }
    this.selectedCustomer.set(customer);
    this.customerQuery.setValue(`${customer.firstName} ${customer.lastName}`, { emitEvent: false });
    this.customerResults.set([]);
    this.form.controls.customerId.setValue(customer.id);
    this.form.controls.cardTypeId.enable();
    this.form.controls.requestedLimit.enable();
    this.result.set(null);
    this.error.set('');
  }

  protected clearCustomer(): void {
    this.selectedCustomer.set(null);
    this.customerQuery.reset('', { emitEvent: false });
    this.customerResults.set([]);
    this.form.reset({ customerId: null, cardTypeId: 1, requestedLimit: null });
    this.form.controls.cardTypeId.disable();
    this.form.controls.requestedLimit.disable();
    this.result.set(null);
    this.error.set('');
  }

  protected clear(): void {
    this.clearCustomer();
    this.customerQuery.markAsUntouched();
  }

  protected simulate(): void {
    if (this.form.invalid || !this.selectedCustomer()) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.isSimulating.set(true);
    this.error.set('');
    this.api.simulate({
      customerId: value.customerId!,
      cardTypeId: value.cardTypeId,
      requestedLimit: value.requestedLimit!,
      deliveryMethod: 'NotApplicable',
    }).subscribe({
      next: result => {
        this.result.set(result);
        this.isSimulating.set(false);
      },
      error: response => {
        this.error.set(response.error?.detail ?? 'Simülasyon çalıştırılamadı.');
        this.isSimulating.set(false);
      },
    });
  }

  protected riskLabel(value: string): string {
    return ({ LOW: 'Düşük Risk', MEDIUM: 'Orta Risk', HIGH: 'Yüksek Risk' } as Record<string, string>)[value] ?? value;
  }

  protected recommendationLabel(value: string): string {
    return ({
      APPROVAL_RECOMMENDED: 'Onay önerilir',
      MANUAL_REVIEW: 'Manuel inceleme',
      CAUTION_RECOMMENDED: 'Dikkatli inceleme',
    } as Record<string, string>)[value] ?? value;
  }
}
