import { Location } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AddressCatalogService, AddressOption } from '../../../core/services/address-catalog.service';
import { CustomerApiService } from '../customer-api.service';
import { Customer, CustomerAddress, CustomerAddressRequest } from '../customer.models';
import {
  addressNamePattern, apartmentNoPattern, buildingNoPattern, floorPattern, postalCodePattern, streetPattern,
} from '../../../core/validators/business-validators';

@Component({
  selector: 'app-customer-addresses',
  imports: [ReactiveFormsModule],
  template: `
    <section class="page">
      <header>
        <div><p>MÜŞTERİ İŞLEMLERİ</p><h1>Kayıtlı Adresler</h1><span>Adresleri isimlendirin, varsayılan teslimat adresini belirleyin ve başvurularda yeniden kullanın.</span></div>
        <a href="" (click)="goBack(); $event.preventDefault()">← Geldiğim Sayfaya Dön</a>
      </header>
      @if (errorMessage()) { <div class="message error"><span>{{ errorMessage() }}</span><button type="button" (click)="errorMessage.set('')">×</button></div> }
      @if (successMessage()) { <div class="message success"><span>{{ successMessage() }}</span><button type="button" (click)="successMessage.set('')">×</button></div> }
      @if (customer(); as item) {
        <article class="customer"><span>{{ item.customerNumber }}</span><b>{{ item.firstName }} {{ item.lastName }}</b><small>{{ addresses().length }} kayıtlı adres</small></article>
      }
      <div class="layout">
        <section class="list-panel">
          <div class="panel-title"><div><h2>Adres Defteri</h2><p>Başvuruda seçilebilen aktif adresler</p></div>@if(canEdit()){<button type="button" (click)="startNew()">+ Yeni Adres</button>}</div>
          <div class="address-list">
            @for (address of addresses(); track address.id) {
              <article [class.default]="address.isDefault">
                <div class="address-head"><div><b>{{ address.name }}</b>@if(address.isDefault){<span>Varsayılan</span>}</div><small>{{ address.postalCode }}</small></div>
                <p>{{ address.fullAddress }}</p>
                @if(canEdit()){
                  <div class="row-actions"><button type="button" (click)="edit(address)">Düzenle</button>@if(!address.isDefault){<button type="button" (click)="makeDefault(address)">Varsayılan Yap</button><button type="button" class="danger" (click)="pendingDelete.set(address)">Kaldır</button>}</div>
                }
              </article>
            } @empty { <div class="empty">Henüz kayıtlı adres bulunmuyor.</div> }
          </div>
        </section>
        @if (canEdit() && editorOpen()) {
          <form [formGroup]="form" (ngSubmit)="save()" class="editor">
            <div class="panel-title"><div><h2>{{ editingAddress() ? 'Adresi Düzenle' : 'Yeni Adres Kaydet' }}</h2><p>Ev, İş veya Yazlık gibi kolay anlaşılır bir ad verin.</p></div></div>
            <label class="wide">Adres Adı *<input formControlName="name" placeholder="Örn. Ev Adresi" maxlength="40">@if(form.controls.name.touched && form.controls.name.invalid){<small>2–40 karakter arasında bir adres adı girin.</small>}</label>
            <div class="grid">
              <label>İl *<select formControlName="city"><option value="">İl seçin</option>@for(item of provinces();track item.id){<option [value]="item.name">{{ item.name }}</option>}</select></label>
              <label>İlçe *<select formControlName="district" [disabled]="!form.controls.city.value"><option value="">İlçe seçin</option>@for(item of districts();track item.id){<option [value]="item.name">{{ item.name }}</option>}</select></label>
              <label>Mahalle *<select formControlName="neighborhood" [disabled]="!form.controls.district.value"><option value="">Mahalle seçin</option>@for(item of neighborhoods();track item.id){<option [value]="item.name">{{ item.name }}</option>}</select></label>
              <label>Cadde / Sokak / Bulvar *<select formControlName="street"><option value="">Adres bileşeni seçin</option>@for(item of streets();track item){<option [value]="item">{{item}}</option>}</select>@if(form.controls.street.touched && form.controls.street.invalid){<small>Ulusal katalogdan geçerli bir cadde/sokak seçin.</small>}</label>
              <label>Site / Mevki Bilgisi<input formControlName="avenue" maxlength="120" placeholder="Varsa site veya mevki adı">@if(form.controls.avenue.touched && form.controls.avenue.invalid){<small>Geçersiz adres karakteri kullanıldı.</small>}</label>
              <label>Bina No *<input formControlName="buildingNo" maxlength="20" placeholder="Örn. 12/A">@if(form.controls.buildingNo.touched && form.controls.buildingNo.invalid){<small>Yalnızca harf, rakam, “-” ve “/” kullanılabilir.</small>}</label>
              <label>Daire No<input formControlName="apartmentNo" maxlength="20" placeholder="Örn. 5-B">@if(form.controls.apartmentNo.touched && form.controls.apartmentNo.invalid){<small>Yalnızca harf, rakam, “-” ve “/” kullanılabilir.</small>}</label>
              <label>Kat<input formControlName="floor" inputmode="numeric" maxlength="4" placeholder="Örn. -1, 0, 12">@if(form.controls.floor.touched && form.controls.floor.invalid){<small>Kat −999 ile 999 arasında tam sayı olmalıdır.</small>}</label>
              <label>Posta Kodu *<input formControlName="postalCode" inputmode="numeric" maxlength="5" readonly>@if(postalCodeStatus()){<small class="catalog-note">{{postalCodeStatus()==='official'?'Resmî mahalle posta kodu':postalCodeStatus()==='derived'?'Türetilmiş mahalle posta kodu':'Tahmini posta kodu — kontrol gerekli'}}</small>}</label>
            </div>
            <div class="preview"><span>OLUŞTURULAN AÇIK ADRES</span><b>{{ composedAddress() || 'Adres alanları tamamlandığında burada görünecektir.' }}</b></div>
            <label class="default-choice"><input type="checkbox" formControlName="isDefault"><span><b>Varsayılan adres yap</b><small>Yeni başvurularda bu adres ilk seçenek olarak gelir.</small></span></label>
            <div class="form-actions"><button type="button" class="secondary" (click)="cancelEdit()">Vazgeç</button><button type="submit" [disabled]="isSaving()">{{ isSaving() ? 'Kaydediliyor...' : 'Adresi Kaydet' }}</button></div>
          </form>
        }
      </div>
    </section>
    @if(pendingDelete(); as address){<div class="overlay" (click)="pendingDelete.set(null)"><section class="modal" (click)="$event.stopPropagation()"><h2>Adresi kaldır</h2><p><b>{{ address.name }}</b> adlı adres başvuru seçimlerinden kaldırılacak. Geçmiş başvurular etkilenmez.</p><div><button type="button" class="secondary" (click)="pendingDelete.set(null)">Vazgeç</button><button type="button" class="danger-solid" (click)="deleteAddress(address)">Adresi Kaldır</button></div></section></div>}
  `,
  styles: `
    :host{display:block}.page{max-width:1200px;margin:auto;padding:32px;color:#17324d}.page>header{display:flex;align-items:flex-end;justify-content:space-between;margin-bottom:20px}.page>header p{margin:0 0 7px;color:#9b761f;font-size:10px;font-weight:900;letter-spacing:.15em}.page>header h1{margin:0;color:#0b2a4d;font-size:30px}.page>header span{display:block;margin-top:7px;color:#738397}.page>header a{color:#355875;font-weight:800;text-decoration:none}.customer{display:flex;align-items:center;gap:12px;margin-bottom:16px;padding:14px 17px;border:1px solid #dce4eb;border-radius:9px;background:#fff}.customer span{color:#9a761e;font-size:11px;font-weight:900}.customer b{font-size:15px}.customer small{margin-left:auto;color:#7b8996}.layout{display:grid;grid-template-columns:minmax(0,1fr) minmax(420px,.85fr);gap:18px;align-items:start}.list-panel,.editor{overflow:hidden;border:1px solid #dce4eb;border-radius:10px;background:#fff}.panel-title{display:flex;align-items:center;justify-content:space-between;padding:18px 20px;border-bottom:1px solid #e7ebef}.panel-title h2{margin:0;font-size:17px}.panel-title p{margin:4px 0 0;color:#7c8997;font-size:11px}.panel-title button,.form-actions button{padding:10px 13px;border:0;border-radius:6px;color:#fff;background:#0b3158;font-weight:800;cursor:pointer}.address-list{display:grid;gap:10px;padding:16px}.address-list article{padding:15px;border:1px solid #dce4eb;border-radius:8px;background:#fafcfd}.address-list article.default{border-color:#d0aa4a;background:#fffaf0}.address-head{display:flex;justify-content:space-between}.address-head>div{display:flex;align-items:center;gap:8px}.address-head span{padding:4px 7px;border-radius:999px;color:#7a590c;background:#f8e9bd;font-size:9px;font-weight:800}.address-head small{color:#7d8995}.address-list p{margin:10px 0;color:#4e6479;line-height:1.5}.row-actions{display:flex;gap:7px}.row-actions button{padding:7px 9px;border:1px solid #cad6df;border-radius:5px;color:#31526e;background:#fff;font-weight:800;cursor:pointer}.row-actions .danger{color:#a13b3b}.empty{padding:35px;color:#7d8a98;text-align:center}.editor{padding-bottom:18px}.editor>.wide,.grid{margin:16px 18px 0}.editor label{display:grid;gap:6px;color:#314b62;font-size:11px;font-weight:800}.editor input,.editor select{box-sizing:border-box;width:100%;padding:10px;border:1px solid #cbd6df;border-radius:6px;background:#fff;font:inherit}.editor label small{color:#a13b3b}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}.preview{display:grid;gap:6px;margin:15px 18px;padding:13px;border-left:4px solid #1b537f;border-radius:6px;background:#f2f7fb}.preview span{color:#778a9b;font-size:9px;font-weight:900}.preview b{font-size:11px;line-height:1.45}.default-choice{display:flex!important;align-items:flex-start;gap:9px;margin:15px 18px;padding:12px;border:1px solid #e0e6eb;border-radius:7px;background:#f9fafb}.default-choice input{width:17px!important}.default-choice span{display:grid;gap:3px}.default-choice small{color:#7d8995!important;font-weight:500}.form-actions{display:flex;justify-content:flex-end;gap:8px;margin:15px 18px 0}.form-actions .secondary{color:#40576d;background:#eaf0f4}.message{display:flex;justify-content:space-between;gap:10px;margin-bottom:14px;padding:12px 15px;border-radius:7px}.message button{border:0;color:inherit;background:transparent;font-size:19px}.message.error{color:#9e3838;background:#fdecec}.message.success{color:#1c6b49;background:#eaf7f0}.overlay{position:fixed;inset:0;z-index:100;display:grid;place-items:center;padding:20px;background:#071c31a8}.modal{width:min(440px,100%);padding:23px;border-radius:11px;background:#fff}.modal h2{margin:0;color:#17324d}.modal p{color:#65798d;line-height:1.55}.modal div{display:flex;justify-content:flex-end;gap:8px}.modal button{padding:10px 13px;border:0;border-radius:6px;font-weight:800}.modal .secondary{color:#3d566d;background:#e9eef3}.modal .danger-solid{color:#fff;background:#a23e3e}@media(max-width:900px){.layout{grid-template-columns:1fr}}@media(max-width:650px){.page{padding:20px}.page>header{align-items:flex-start;flex-direction:column;gap:14px}.grid{grid-template-columns:1fr}}
  `,
})
export class CustomerAddresses implements OnInit {
  protected customerId = 0;
  protected readonly customer = signal<Customer | null>(null);
  protected readonly addresses = signal<CustomerAddress[]>([]);
  protected readonly provinces = signal<AddressOption[]>([]);
  protected readonly districts = signal<AddressOption[]>([]);
  protected readonly neighborhoods = signal<AddressOption[]>([]);
  protected readonly streets = signal<string[]>([]);
  protected readonly postalCodeStatus = signal<'official' | 'derived' | 'estimated' | ''>('');
  protected readonly editorOpen = signal(false);
  protected readonly editingAddress = signal<CustomerAddress | null>(null);
  protected readonly pendingDelete = signal<CustomerAddress | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly isSaving = signal(false);
  protected readonly canEdit = computed(() => this.auth.currentUser()?.role === 'Officer');
  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(40), Validators.pattern(addressNamePattern)] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    district: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    neighborhood: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    street: new FormControl({ value: '', disabled: true }, { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120), Validators.pattern(streetPattern)] }),
    avenue: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120), Validators.pattern(streetPattern)] }),
    buildingNo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20), Validators.pattern(buildingNoPattern)] }),
    apartmentNo: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(20), Validators.pattern(apartmentNoPattern)] }),
    floor: new FormControl('', { nonNullable: true, validators: [Validators.pattern(floorPattern)] }),
    postalCode: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(postalCodePattern)] }),
    isDefault: new FormControl(false, { nonNullable: true }),
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CustomerApiService,
    private readonly catalog: AddressCatalogService,
    protected readonly auth: AuthService,
    private readonly location: Location,
    destroyRef: DestroyRef,
  ) {
    this.form.controls.city.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(city => this.loadDistricts(city));
    this.form.controls.district.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(district => this.loadNeighborhoods(district));
    this.form.controls.neighborhood.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(neighborhood => this.loadNeighborhoodAddressData(neighborhood));
  }

  ngOnInit(): void {
    this.customerId = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(this.customerId) || this.customerId <= 0) { this.errorMessage.set('Geçersiz müşteri numarası.'); return; }
    this.api.getById(this.customerId).subscribe({ next: value => this.customer.set(value), error: () => this.errorMessage.set('Müşteri bilgileri yüklenemedi.') });
    this.catalog.getProvinces().subscribe({ next: value => this.provinces.set(value), error: () => this.errorMessage.set('Adres seçim listesi yüklenemedi.') });
    this.reload();
  }

  protected goBack(): void { this.location.back(); }
  protected startNew(): void { this.editingAddress.set(null); this.form.reset({ isDefault: !this.addresses().length }); this.form.controls.street.disable({emitEvent:false}); this.districts.set([]); this.neighborhoods.set([]); this.streets.set([]); this.postalCodeStatus.set(''); this.editorOpen.set(true); }
  protected cancelEdit(): void { this.editorOpen.set(false); this.editingAddress.set(null); }
  protected edit(address: CustomerAddress): void {
    this.editingAddress.set(address); this.editorOpen.set(true);
    this.form.setValue({ name: address.name, city: address.city, district: address.district,
      neighborhood: address.neighborhood, street: address.street, avenue: address.avenue ?? '',
      buildingNo: address.buildingNo, apartmentNo: address.apartmentNo ?? '', floor: address.floor ?? '',
      postalCode: address.postalCode, isDefault: address.isDefault }, { emitEvent: false });
    this.loadAddressDependencies(address);
  }
  protected composedAddress(): string {
    const v = this.form.getRawValue();
    return [v.neighborhood, v.street, v.avenue, v.buildingNo ? `Bina No: ${v.buildingNo}` : '',
      v.apartmentNo ? `Daire: ${v.apartmentNo}` : '', v.floor ? `Kat: ${v.floor}` : '',
      v.postalCode, v.district && v.city ? `${v.district} / ${v.city}` : ''].filter(Boolean).join(', ');
  }
  protected save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const request = this.toRequest(); const current = this.editingAddress(); this.isSaving.set(true); this.errorMessage.set('');
    const operation = current ? this.api.updateAddress(this.customerId, current.id, request) : this.api.addAddress(this.customerId, request);
    operation.subscribe({ next: () => { this.isSaving.set(false); this.successMessage.set(current ? 'Adres güncellendi.' : 'Adres kaydedildi ve başvurularda seçilebilir hâle getirildi.'); this.cancelEdit(); this.reload(); }, error: response => { this.isSaving.set(false); this.errorMessage.set(response.error?.detail ?? 'Adres kaydedilemedi.'); } });
  }
  protected makeDefault(address: CustomerAddress): void { this.api.setDefaultAddress(this.customerId, address.id).subscribe({ next: () => { this.successMessage.set(`${address.name} varsayılan adres yapıldı.`); this.reload(); }, error: response => this.errorMessage.set(response.error?.detail ?? 'Varsayılan adres değiştirilemedi.') }); }
  protected deleteAddress(address: CustomerAddress): void { this.api.deleteAddress(this.customerId, address.id).subscribe({ next: () => { this.pendingDelete.set(null); this.successMessage.set('Adres kaldırıldı.'); this.reload(); }, error: response => { this.pendingDelete.set(null); this.errorMessage.set(response.error?.detail ?? 'Adres kaldırılamadı.'); } }); }

  private reload(): void { this.api.getAddresses(this.customerId).subscribe({ next: value => this.addresses.set(value), error: () => this.errorMessage.set('Kayıtlı adresler yüklenemedi.') }); }
  private toRequest(): CustomerAddressRequest { const v = this.form.getRawValue(); return { ...v, avenue: v.avenue.trim() || null, apartmentNo: v.apartmentNo.trim() || null, floor: v.floor.trim() || null }; }
  private loadDistricts(cityName: string): void { this.districts.set([]); this.neighborhoods.set([]); this.streets.set([]); this.postalCodeStatus.set(''); this.form.patchValue({district:'',neighborhood:'',street:'',postalCode:''},{emitEvent:false}); this.form.controls.street.disable({emitEvent:false}); const city = this.provinces().find(x => x.name === cityName); if (city) this.catalog.getDistricts(city.id).subscribe(value => this.districts.set(value)); }
  private loadNeighborhoods(districtName: string): void { this.neighborhoods.set([]); this.streets.set([]); this.postalCodeStatus.set(''); this.form.patchValue({neighborhood:'',street:'',postalCode:''},{emitEvent:false}); this.form.controls.street.disable({emitEvent:false}); const district = this.districts().find(x => x.name === districtName); if (district) this.catalog.getNeighborhoods(district.id).subscribe(value => this.neighborhoods.set(value)); }
  private loadNeighborhoodAddressData(neighborhoodName: string): void { this.streets.set([]); this.form.patchValue({street:'',postalCode:''},{emitEvent:false}); this.form.controls.street.disable({emitEvent:false}); const neighborhood=this.neighborhoods().find(x=>x.name===neighborhoodName); this.postalCodeStatus.set(neighborhood?.postalCodeStatus??''); this.form.controls.postalCode.setValue(neighborhood?.postalCode??'',{emitEvent:false}); if(!neighborhood)return; this.catalog.getStreets(neighborhood.id).subscribe({next:items=>{this.streets.set(items.map(x=>x.name));this.form.controls.street.enable({emitEvent:false});},error:()=>this.errorMessage.set('Ulusal cadde/sokak kataloğu yüklenemedi.')}); }
  private loadAddressDependencies(address: CustomerAddress): void { const province = this.provinces().find(x => x.name === address.city); if (!province) return; this.catalog.getDistricts(province.id).subscribe(districts => { this.districts.set(districts); const district = districts.find(x => x.name === address.district); if (district) this.catalog.getNeighborhoods(district.id).subscribe(items => {this.neighborhoods.set(items);const neighborhood=items.find(x=>x.name===address.neighborhood);this.postalCodeStatus.set(neighborhood?.postalCodeStatus??'');if(neighborhood)this.catalog.getStreets(neighborhood.id).subscribe(streets=>{this.streets.set(streets.map(x=>x.name));this.form.controls.street.enable({emitEvent:false});});}); }); }
}
