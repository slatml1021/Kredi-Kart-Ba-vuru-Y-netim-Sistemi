import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import {
  PlatformApiService,
  SupplementaryApplication,
  SupplementaryCardContext,
} from '../../core/services/platform-api.service';
import { CustomerApiService } from '../customers/customer-api.service';
import { Customer, CustomerAddress, CustomerCardSummary } from '../customers/customer.models';
import { AddressCatalogService, AddressOption } from '../../core/services/address-catalog.service';
import {
  addressNamePattern, apartmentNoPattern, buildingNoPattern, floorPattern, postalCodePattern, streetPattern,
} from '../../core/validators/business-validators';

type DeliveryMethod = 'RegisteredAddress' | 'Branch' | 'DifferentAddress';

@Component({
  selector: 'app-supplementary-applications',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  template: `
    <section class="page" [class.embedded]="embedded()">
      @if (!embedded()) {
        <p>EK KART YÖNETİMİ</p>
        <h1>Ek Kredi Kartı Başvuruları</h1>
        <span>Ana kart limitini paylaşan ek kartları kurumsal başvuru adımlarıyla yönetin.</span>
      }

      @if (message()) {
        <div class="message" role="status">
          <span>{{ message() }}</span>
          <button type="button" (click)="message.set('')" aria-label="Bildirimi kapat">×</button>
        </div>
      }

      @if (!isManager()) {
        <form [formGroup]="form" (ngSubmit)="showSummary()">
          <div class="section-title">
            <div><small>01</small><h2>Ana Kart ve Müşteri</h2></div>
            <span>Kart bilgileri kredi kartı detayından güvenli şekilde aktarılır.</span>
          </div>

          @if (cardContext(); as context) {
            <div class="context-grid">
              <div><span>MÜŞTERİ NO</span><b>{{ context.primaryCustomerNumber }}</b></div>
              <div><span>AD SOYAD</span><b>{{ context.primaryCustomerFullName }}</b></div>
              <div><span>TC KİMLİK NO</span><b>{{ context.primaryCustomerNationalIdentityNumber }}</b></div>
              <div><span>MÜŞTERİ DURUMU</span><b [class.passive]="!context.primaryCustomerIsActive">{{ context.primaryCustomerIsActive ? 'Aktif' : 'Pasif' }}</b></div>
              <div><span>ANA KART</span><b>{{ context.maskedCardNumber }}</b></div>
              <div><span>KART DURUMU</span><b>{{ cardStatus(context.primaryCardStatus) }}</b></div>
              <div><span>ANA KART LİMİTİ</span><b>{{ context.primaryCardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
              <div><span>KULLANILAN / TAHSİS EDİLEN</span><b>{{ context.usedSupplementaryLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
              <div class="available"><span>KULLANILABİLİR LİMİT</span><b>{{ context.availableSupplementaryLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
            </div>
          } @else {
            <div class="helper"><b>Ana kart sahibini ve kartı bu ekrandan seçebilirsiniz.</b> İsterseniz kredi kartı detayındaki “Ek Kart Başvurusu” düğmesini de kullanabilirsiniz.</div>
            <div class="holder-search primary-search">
              <label>Ana Kart Sahibi<input [formControl]="primaryQuery" placeholder="TC Kimlik No, müşteri no veya ad soyad"></label>
              <button type="button" (click)="searchPrimaryCustomer()" [disabled]="primaryQuery.invalid">Müşteri Sorgula</button>
            </div>
            @if (primaryQuery.touched && primaryQuery.invalid) { <small class="validation">En az 2 karakterlik bir müşteri arama kriteri girin.</small> }
            @if (primaryResults().length) {
              <div class="holder-results">@for (customer of primaryResults(); track customer.id) {
                <button type="button" (click)="selectPrimaryCustomer(customer)"><span><b>{{ customer.firstName }} {{ customer.lastName }}</b><small>{{ customer.customerNumber }} · {{ customer.nationalIdentityNumber }}</small></span><em>{{ customer.isActive ? 'Kartları Gör →' : 'Pasif' }}</em></button>
              }</div>
            }
            @if (primaryCards().length) {
              <div class="primary-cards"><span>ANA KARTI SEÇİN</span>@for (card of primaryCards(); track card.id) {
                <button type="button" (click)="selectPrimaryCard(card)"><b>{{ card.cardTypeName }}</b><small>{{ card.maskedCardNumber }} · {{ card.cardLimit | currency:'TRY':'symbol-narrow':'1.0-0' }} · {{ cardStatus(card.status) }}</small></button>
              }</div>
            }
          }

          <div class="section-title">
            <div><small>02</small><h2>Ek Kart Sahibi</h2></div>
            <span>TC Kimlik No, müşteri no veya ad soyad ile sorgulayın.</span>
          </div>
          <div class="holder-search">
            <label>Ek Kart Sahibi
              <input [formControl]="holderQuery" placeholder="TC Kimlik No, müşteri no veya ad soyad">
            </label>
            <button type="button" (click)="searchHolder()" [disabled]="holderQuery.invalid">Müşteri Sorgula</button>
          </div>
          @if (holderQuery.touched && holderQuery.invalid) {
            <small class="validation">En az 2 karakterlik bir müşteri arama kriteri girin.</small>
          }
          @if (holderResults().length) {
            <div class="holder-results">
              @for (customer of holderResults(); track customer.id) {
                <button type="button" (click)="selectHolder(customer)">
                  <span><b>{{ customer.firstName }} {{ customer.lastName }}</b><small>{{ customer.customerNumber }} · {{ customer.nationalIdentityNumber }}</small></span>
                  <em>{{ customer.isActive ? 'Seç →' : 'Pasif' }}</em>
                </button>
              }
            </div>
          }
          @if (selectedHolder(); as holder) {
            <div class="selected-holder">
              <div><span>EK KART SAHİBİ</span><b>{{ holder.firstName }} {{ holder.lastName }}</b></div>
              <div><span>MÜŞTERİ NO</span><b>{{ holder.customerNumber }}</b></div>
              <div><span>TC KİMLİK NO</span><b>{{ holder.nationalIdentityNumber }}</b></div>
              <div><span>DURUM</span><b>{{ holder.isActive ? 'Aktif' : 'Pasif' }}</b></div>
              <button type="button" (click)="clearHolder()">Değiştir</button>
            </div>
          }

          <div class="section-title">
            <div><small>03</small><h2>Başvuru Tercihleri</h2></div>
            <span>Yakınlık, limit ve teslimat yöntemini belirleyin.</span>
          </div>
          <div class="grid">
            <label>Yakınlık Derecesi *
              <select formControlName="relationship">
                <option value="">Seçin</option>
                <optgroup label="Birinci derece / aile"><option>Eş</option><option>Anne</option><option>Baba</option><option>Çocuk</option></optgroup>
                <optgroup label="İkinci derece"><option>Kardeş</option></optgroup>
                <optgroup label="Diğer"><option>Diğer</option></optgroup>
              </select>
              @if (relationshipAgeNotice()) { <small class="relationship-note">{{ relationshipAgeNotice() }}</small> }
            </label>
            <label>Ek Kart Limiti *
              <input type="number" min="1" step="500" placeholder="Örn. 10.000" formControlName="requestedLimit">
              @if (cardContext(); as context) {
                <small>En fazla {{ context.availableSupplementaryLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</small>
              }
            </label>
          </div>
          @if (limitExceeded()) {
            <div class="inline-error">Girilen limit kullanılabilir limiti aşamaz.</div>
          }

          <fieldset class="delivery-options">
            <legend>Teslimat Yöntemi *</legend>
            @for (option of deliveryOptions; track option.value) {
              <label [class.selected]="form.controls.deliveryMethod.value === option.value">
                <input type="radio" formControlName="deliveryMethod" [value]="option.value">
                <span><b>{{ option.label }}</b><small>{{ option.description }}</small></span>
              </label>
            }
          </fieldset>
          @if (form.controls.deliveryMethod.value === 'RegisteredAddress' && cardContext(); as context) {
            <div class="registered-address"><span>KAYITLI TESLİMAT ADRESİ</span>
              @if(savedAddresses().length){<label>Adres Seç<select formControlName="selectedSavedAddressId" (change)="selectSavedAddress(+$any($event.target).value)">@for(address of savedAddresses();track address.id){<option [value]="address.id">{{ address.name }}{{ address.isDefault ? ' · Varsayılan' : '' }}</option>}</select></label>}
              <b>{{ deliveryAddressLabel() || context.registeredDeliveryAddress }}</b>
              <a [routerLink]="['/officer/customers', context.primaryCustomerId, 'addresses']">Kayıtlı adresleri yönet →</a>
            </div>
          }
          @if (form.controls.deliveryMethod.value === 'Branch') {
            <div class="address-grid">
              <label>Şube İli *<select formControlName="deliveryProvince"><option value="">İl seçin</option>@for (city of branchProvinceNames; track city) { <option [value]="city">{{ city }}</option> }</select></label>
              <label>Şube İlçesi *<select formControlName="deliveryDistrict"><option value="">İlçe seçin</option>@for (district of availableBranchDistricts(); track district) { <option [value]="district">{{ district }}</option> }</select></label>
              <label class="wide">Teslimat Şubesi *<select formControlName="deliveryBranch"><option value="">Şube seçin</option>@for (branch of availableBranches(); track branch) { <option [value]="branch">{{ branch }}</option> }</select></label>
            </div>
          }
          @if (form.controls.deliveryMethod.value === 'DifferentAddress') {
            <div class="address-grid">
              <label>İl *<select formControlName="deliveryProvince"><option value="">İl seçin</option>@for (province of provinces(); track province.id) { <option [value]="province.name">{{ province.name }}</option> }</select></label>
              <label>İlçe *<select formControlName="deliveryDistrict"><option value="">İlçe seçin</option>@for (district of districts(); track district.id) { <option [value]="district.name">{{ district.name }}</option> }</select></label>
              <label>Mahalle *<select formControlName="deliveryNeighborhood"><option value="">Mahalle seçin</option>@for (neighborhood of neighborhoods(); track neighborhood.id) { <option [value]="neighborhood.name">{{ neighborhood.name }}</option> }</select></label>
              <label>Sokak / Cadde / Bulvar *<select formControlName="deliveryStreet"><option value="">Adres bileşeni seçin</option>@for (street of streets(); track street) { <option [value]="street">{{ street }}</option> }</select>@if(form.controls.deliveryStreet.touched && form.controls.deliveryStreet.invalid){<small class="validation">Ulusal katalogdan geçerli bir cadde/sokak seçin.</small>}</label>
              <label>Site / Mevki Bilgisi<input formControlName="deliveryAvenue" maxlength="120" placeholder="Varsa site veya mevki adı">@if(form.controls.deliveryAvenue.touched && form.controls.deliveryAvenue.invalid){<small class="validation">Yalnızca geçerli adres karakterleri kullanılabilir.</small>}</label>
              <label>Bina No *<input formControlName="deliveryBuildingNo" maxlength="20" placeholder="Örn. 12/A">@if(form.controls.deliveryBuildingNo.touched && form.controls.deliveryBuildingNo.invalid){<small class="validation">Yalnızca harf, rakam, “-” ve “/” kullanılabilir.</small>}</label>
              <label>Daire No<input formControlName="deliveryApartmentNo" maxlength="20" placeholder="Örn. 5-B">@if(form.controls.deliveryApartmentNo.touched && form.controls.deliveryApartmentNo.invalid){<small class="validation">Yalnızca harf, rakam, “-” ve “/” kullanılabilir.</small>}</label>
              <label>Kat<input formControlName="deliveryFloor" inputmode="numeric" maxlength="4" placeholder="Örn. -1, 0, 12">@if(form.controls.deliveryFloor.touched && form.controls.deliveryFloor.invalid){<small class="validation">Kat yalnızca -2 ile 99 arasında tam sayı olabilir.</small>}</label>
              <label>Posta Kodu *<input formControlName="deliveryPostalCode" inputmode="numeric" maxlength="5" readonly>@if(form.controls.deliveryPostalCode.touched && form.controls.deliveryPostalCode.invalid){<small class="validation">Mahalle kataloğundan gelen 5 haneli posta kodu zorunludur.</small>}@else if(postalCodeStatus()){<small>{{ postalCodeStatus()==='official'?'Mahalle posta kodu':postalCodeStatus()==='derived'?'Katalogdan türetilen posta kodu':'Posta kodu — kontrol önerilir' }}</small>}</label>
              <div class="composed-address wide"><span>OLUŞTURULAN AÇIK ADRES</span><b>{{ composedDifferentAddress() || 'Adres seçimleri tamamlandığında otomatik oluşacaktır.' }}</b></div>
              <div class="save-address wide"><label><input type="checkbox" formControlName="saveDeliveryAddress"><span><b>Bu adresi müşterinin adres defterine kaydet</b><small>Sonraki başvurularda doğrudan seçilebilir.</small></span></label>@if(form.controls.saveDeliveryAddress.value){<label>Adres Adı *<input formControlName="addressName" maxlength="40" placeholder="Örn. Yazlık Adresi">@if(form.controls.addressName.touched && form.controls.addressName.invalid){<small class="validation">2–40 karakterlik geçerli bir adres adı girin.</small>}</label>}</div>
            </div>
          }

          <div class="form-actions">
            <button type="button" class="secondary" (click)="resetForm()">Temizle</button>
            <button type="submit" [disabled]="isSubmitting()">Başvuru Özetini Gör</button>
          </div>
        </form>

        @if (summaryVisible() && cardContext(); as context) {
          <section class="summary" aria-label="Ek kart başvuru özeti">
            <div class="section-title"><div><small>04</small><h2>Başvuru Özeti</h2></div><span>Oluşturmadan önce bilgileri son kez kontrol edin.</span></div>
            <dl>
              <div><dt>Ana Kart</dt><dd>{{ context.maskedCardNumber }}</dd></div>
              <div><dt>Ek Kart Sahibi</dt><dd>{{ selectedHolder()?.firstName }} {{ selectedHolder()?.lastName }}</dd></div>
              <div><dt>Yakınlık</dt><dd>{{ form.controls.relationship.value }}</dd></div>
              <div><dt>Ek Kart Limiti</dt><dd>{{ form.controls.requestedLimit.value | currency:'TRY':'symbol-narrow':'1.0-0' }}</dd></div>
              <div><dt>Teslimat Şekli</dt><dd>{{ deliveryLabel(form.controls.deliveryMethod.value) }}</dd></div>
              <div><dt>Teslimat Adresi / Şube</dt><dd>{{ deliveryAddressLabel() }}</dd></div>
            </dl>
            <div class="form-actions">
              <button type="button" class="secondary" (click)="summaryVisible.set(false)">Bilgileri Düzenle</button>
              <button type="button" (click)="create()" [disabled]="isSubmitting()">{{ isSubmitting() ? 'Oluşturuluyor...' : 'Ek Kart Başvurusu Oluştur' }}</button>
            </div>
          </section>
        }
      }

      <article>
        <h2>{{ isManager() ? 'Değerlendirme Kuyruğu' : 'Ek Kart Başvurularım' }}</h2>
        <div class="table"><table><thead><tr><th>BAŞVURU</th><th>ANA KART SAHİBİ</th><th>EK KART SAHİBİ</th><th>YAKINLIK</th><th>LİMİT</th><th>DURUM</th><th>İŞLEM</th></tr></thead>
        <tbody>@for (item of applications(); track item.id) { <tr><td>{{ item.applicationNumber }}<small>{{ item.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</small></td><td>{{ item.primaryCustomer }}</td><td>{{ item.holderCustomer }}</td><td>{{ item.relationship }}</td><td>{{ item.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</td><td><b [class]="item.status.toLowerCase()">{{ status(item.status) }}</b></td><td><a class="detail-link" [routerLink]="[isManager() ? '/manager/supplementary-applications' : '/officer/supplementary-applications', item.id]">İncele →</a></td></tr> } @empty { <tr><td colspan="7">Kayıt bulunmuyor.</td></tr> }</tbody></table></div>
      </article>
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1200px;margin:auto;padding:32px;color:#20364d}.page>p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.16em}h1{margin:7px 0 4px;color:#0b2a4d;font-size:31px}.page>span{color:#718093}.message{display:flex;justify-content:space-between;align-items:center;margin:16px 0;padding:12px;border-radius:8px;background:#edf6ff;color:#174d78}.message button{margin:0;padding:0 5px;color:inherit;background:transparent;font-size:20px}form,article,.summary{margin-top:22px;padding:22px;border:1px solid #dce3e9;border-radius:11px;background:#fff}h2{margin:0;color:#1c354e}.section-title{display:flex;justify-content:space-between;align-items:end;margin:4px 0 15px;padding-top:18px;border-top:1px solid #edf1f4}.section-title:first-child{padding-top:0;border-top:0}.section-title>div{display:flex;align-items:center;gap:9px}.section-title small{width:30px;height:30px;display:grid;place-items:center;border-radius:50%;color:#916b12;background:#fff3d5;font-weight:900}.section-title>span{color:#718093;font-size:11px}.context-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:9px;margin-bottom:24px}.context-grid div,.selected-holder div{display:grid;gap:5px;padding:13px;border:1px solid #e0e6eb;border-radius:7px;background:#f8fafb}.context-grid span,.selected-holder span,.registered-address span,.composed-address span{color:#7b8997;font-size:9px;font-weight:800;letter-spacing:.04em}.context-grid b,.selected-holder b{font-size:12px}.context-grid .available{border-color:#d6b660;background:#fff9e9}.passive{color:#a52b2b}.helper{margin-bottom:18px;padding:12px;border-radius:7px;background:#fff7df;color:#70591e}.holder-search{display:grid;grid-template-columns:1fr auto;gap:10px;align-items:end}.holder-search button{height:41px;margin:0}.holder-results{display:grid;gap:6px;margin:10px 0}.holder-results button{display:flex;justify-content:space-between;align-items:center;margin:0;color:#20364d;border:1px solid #dce3e9;background:#fff;text-align:left}.holder-results button span{display:grid;gap:3px}.holder-results small{color:#718093}.holder-results em{color:#174d78;font-style:normal;font-weight:800}.selected-holder{position:relative;display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:12px 0 22px}.selected-holder button{position:absolute;right:8px;bottom:-11px;margin:0;padding:6px 9px}.grid,.address-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:13px}.address-grid{margin:14px 0}.address-grid .wide{grid-column:1/-1}.composed-address{display:grid;gap:6px;padding:13px;border-left:4px solid #1c537d;border-radius:5px;background:#f1f6fa}.composed-address b{font-size:12px;line-height:1.5}label{display:grid;gap:6px;font-weight:700}input,select,textarea{padding:11px;border:1px solid #cad5df;border-radius:7px;font:inherit}label small{color:#718093;font-weight:500}.validation,.inline-error{display:block;margin:7px 0;color:#a52b2b}.inline-error{padding:9px;border-radius:6px;background:#fdecec}.delivery-options{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin:18px 0;padding:0;border:0}.delivery-options legend{grid-column:1/-1;margin-bottom:8px;font-weight:800}.delivery-options label{display:flex;gap:10px;padding:13px;border:1px solid #d8e0e7;border-radius:8px}.delivery-options label.selected{border-color:#b88918;background:#fff9e9}.delivery-options input{align-self:start}.delivery-options span{display:grid;gap:4px}.delivery-options small{color:#718093}.registered-address{display:grid;gap:7px;margin-bottom:12px;padding:13px;border-left:4px solid #1c537d;background:#f1f6fa}.registered-address label{max-width:430px}.registered-address a{color:#174d78;font-size:10px;font-weight:800;text-decoration:none}.save-address{display:grid!important;grid-template-columns:1fr 1fr;gap:12px;padding:13px;border:1px solid #dce4ea;border-radius:7px;background:#f8fafb}.save-address>label:first-child{display:flex;align-items:flex-start;gap:8px}.save-address>label:first-child input{width:17px}.save-address span{display:grid;gap:3px}.form-actions{display:flex;justify-content:flex-end;gap:8px;margin-top:18px}.form-actions button{margin:0}.summary dl{display:grid;grid-template-columns:repeat(3,1fr);gap:9px;margin:0}.summary dl div{padding:13px;border:1px solid #e0e6eb;border-radius:7px;background:#f8fafb}.summary dt{color:#718093;font-size:9px}.summary dd{margin:5px 0 0;font-weight:800}button{margin-top:15px;padding:10px 14px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800;cursor:pointer}button:disabled{cursor:not-allowed;opacity:.5}button.secondary{color:#40566c;background:#e9eef3}.table{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:12px;border-bottom:1px solid #edf0f3;text-align:left;white-space:nowrap;font-size:11px}th{color:#718093;font-size:8px}td small{display:block;color:#8290a0}.pending{color:#8a6410}.approved{color:#17734d}.rejected{color:#a52b2b}.detail-link{color:#0b3d6d;font-weight:800;text-decoration:none}@media(max-width:850px){.context-grid,.selected-holder{grid-template-columns:repeat(2,1fr)}.delivery-options,.summary dl{grid-template-columns:1fr}}@media(max-width:700px){.page{padding:20px}.grid,.address-grid,.holder-search,.context-grid,.selected-holder,.save-address{grid-template-columns:1fr}.address-grid .wide{grid-column:auto}.section-title{align-items:flex-start;flex-direction:column;gap:7px}}
  `,
})
export class SupplementaryApplications implements OnInit {
  readonly embedded = input(false);
  private readonly branchCatalog = [
    { city: 'İstanbul', district: 'Ümraniye', name: 'Finanskent Şubesi' },
    { city: 'İstanbul', district: 'Ataşehir', name: 'Ataşehir Şubesi' },
    { city: 'İstanbul', district: 'Kadıköy', name: 'Kadıköy Şubesi' },
    { city: 'İstanbul', district: 'Üsküdar', name: 'Üsküdar Şubesi' },
    { city: 'İstanbul', district: 'Beşiktaş', name: 'Levent Şubesi' },
    { city: 'Ankara', district: 'Çankaya', name: 'Çankaya Şubesi' },
    { city: 'Ankara', district: 'Yenimahalle', name: 'Batıkent Şubesi' },
    { city: 'İzmir', district: 'Konak', name: 'Konak Şubesi' },
    { city: 'İzmir', district: 'Karşıyaka', name: 'Karşıyaka Şubesi' },
    { city: 'Bursa', district: 'Nilüfer', name: 'Nilüfer Şubesi' },
    { city: 'Antalya', district: 'Muratpaşa', name: 'Muratpaşa Şubesi' },
  ];
  protected readonly applications = signal<SupplementaryApplication[]>([]);
  protected readonly message = signal('');
  protected readonly cardContext = signal<SupplementaryCardContext | null>(null);
  protected readonly primaryQuery = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2)],
  });
  protected readonly primaryResults = signal<Customer[]>([]);
  protected readonly primaryCards = signal<CustomerCardSummary[]>([]);
  protected readonly holderQuery = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2)],
  });
  protected readonly holderResults = signal<Customer[]>([]);
  protected readonly selectedHolder = signal<Customer | null>(null);
  protected readonly summaryVisible = signal(false);
  protected readonly isSubmitting = signal(false);
  protected readonly isManager = computed(() => this.auth.currentUser()?.role === 'Manager');
  protected readonly deliveryOptions: { value: DeliveryMethod; label: string; description: string }[] = [
    { value: 'RegisteredAddress', label: 'Kayıtlı Adres', description: 'Ana kart sahibinin kayıtlı adresi kullanılır.' },
    { value: 'Branch', label: 'Şubeden Teslim', description: 'Ek kart seçilen banka şubesinden teslim alınır.' },
    { value: 'DifferentAddress', label: 'Farklı Adres', description: 'Başvuruya özel teslimat adresi girilir.' },
  ];
  protected readonly provinces = signal<AddressOption[]>([]);
  protected readonly districts = signal<AddressOption[]>([]);
  protected readonly neighborhoods = signal<AddressOption[]>([]);
  protected readonly streets = signal<string[]>([]);
  protected readonly postalCodeStatus = signal<'official' | 'derived' | 'estimated' | ''>('');
  protected readonly savedAddresses = signal<CustomerAddress[]>([]);
  protected readonly branchProvinceNames = [...new Set(this.branchCatalog.map(x => x.city))].sort((a, b) => a.localeCompare(b, 'tr'));
  protected readonly form = new FormGroup({
    primaryCreditCardId: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    supplementaryHolderCustomerId: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    relationship: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    requestedLimit: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    deliveryMethod: new FormControl<DeliveryMethod>('RegisteredAddress', { nonNullable: true, validators: [Validators.required] }),
    deliveryAddress: new FormControl('', { nonNullable: true }),
    deliveryProvince: new FormControl('', { nonNullable: true }),
    deliveryDistrict: new FormControl({ value: '', disabled: true }, { nonNullable: true }),
    deliveryNeighborhood: new FormControl({ value: '', disabled: true }, { nonNullable: true }),
    deliveryStreet: new FormControl({ value: '', disabled: true }, { nonNullable: true, validators: [Validators.minLength(2), Validators.maxLength(120), Validators.pattern(streetPattern)] }),
    deliveryAvenue: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120), Validators.pattern(streetPattern)] }),
    deliveryBuildingNo: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(20), Validators.pattern(buildingNoPattern)] }),
    deliveryApartmentNo: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(20), Validators.pattern(apartmentNoPattern)] }),
    deliveryFloor: new FormControl('', { nonNullable: true, validators: [Validators.pattern(floorPattern)] }),
    deliveryPostalCode: new FormControl('', { nonNullable: true, validators: [Validators.pattern(postalCodePattern)] }),
    deliveryBranch: new FormControl({ value: '', disabled: true }, { nonNullable: true }),
    selectedSavedAddressId: new FormControl(0, { nonNullable: true }),
    addressName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(40), Validators.pattern(addressNamePattern)] }),
    saveDeliveryAddress: new FormControl(false, { nonNullable: true }),
  });

  constructor(
    private readonly api: PlatformApiService,
    private readonly auth: AuthService,
    private readonly route: ActivatedRoute,
    private readonly customerApi: CustomerApiService,
    private readonly addressCatalog: AddressCatalogService,
    destroyRef: DestroyRef,
  ) {
    this.form.controls.deliveryProvince.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(value => this.loadDistricts(value));
    this.form.controls.deliveryDistrict.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(value => this.loadNeighborhoods(value));
    this.form.controls.deliveryNeighborhood.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(value => this.loadNeighborhoodAddressData(value));
    this.form.controls.deliveryMethod.valueChanges.pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => this.resetDeliverySelection());
  }

  ngOnInit(): void {
    this.addressCatalog.getProvinces().subscribe({
      next: items => this.provinces.set(items),
      error: () => this.message.set('Adres seçim listesi yüklenemedi.'),
    });
    const primaryCardId = Number(this.route.snapshot.queryParamMap.get('primaryCardId'));
    if (Number.isInteger(primaryCardId) && primaryCardId > 0) {
      this.form.controls.primaryCreditCardId.setValue(primaryCardId);
      this.api.supplementaryCardContext(primaryCardId).subscribe({
        next: context => { this.cardContext.set(context); this.loadSavedAddresses(context.primaryCustomerId); },
        error: response => this.message.set(response.error?.detail ?? 'Ana kart bilgileri yüklenemedi.'),
      });
    }
    this.load();
  }

  protected searchPrimaryCustomer(): void {
    if (this.primaryQuery.invalid) {
      this.primaryQuery.markAsTouched();
      return;
    }
    this.customerApi.search(this.primaryQuery.value.trim()).subscribe({
      next: customers => this.primaryResults.set(customers),
      error: response => this.message.set(response.error?.detail ?? 'Ana kart sahibi sorgulanamadı.'),
    });
  }

  protected selectPrimaryCustomer(customer: Customer): void {
    if (!customer.isActive) {
      this.message.set('Pasif müşterinin kartı üzerinden ek kart başvurusu oluşturulamaz.');
      return;
    }
    this.customerApi.getDetailById(customer.id).subscribe({
      next: detail => {
        const usableCards = detail.cards.filter(card => ['Active', '2'].includes(card.status));
        this.primaryCards.set(usableCards);
        this.primaryResults.set([]);
        if (!usableCards.length) this.message.set('Seçilen müşterinin ek karta uygun aktif bir ana kartı bulunmuyor.');
      },
      error: response => this.message.set(response.error?.detail ?? 'Müşterinin kartları yüklenemedi.'),
    });
  }

  protected selectPrimaryCard(card: CustomerCardSummary): void {
    this.form.controls.primaryCreditCardId.setValue(card.id);
    this.api.supplementaryCardContext(card.id).subscribe({
      next: context => {
        this.cardContext.set(context);
        this.primaryCards.set([]);
        this.primaryQuery.reset('');
        this.loadSavedAddresses(context.primaryCustomerId);
        this.clearHolder();
        this.message.set('Ana kart seçildi. Şimdi ek kart sahibini belirleyebilirsiniz.');
      },
      error: response => {
        this.form.controls.primaryCreditCardId.reset(null);
        this.cardContext.set(null);
        this.message.set(response.error?.detail ?? 'Ana kart başvuruya bağlanamadı.');
      },
    });
  }

  protected searchHolder(): void {
    if (this.holderQuery.invalid) {
      this.holderQuery.markAsTouched();
      return;
    }
    this.customerApi.search(this.holderQuery.value.trim()).subscribe({
      next: customers => {
        const primaryCustomerId = this.cardContext()?.primaryCustomerId;
        this.holderResults.set(customers.filter(customer => customer.id !== primaryCustomerId));
        if (customers.some(customer => customer.id === primaryCustomerId) && customers.length === 1)
          this.message.set('Kişi kendisi adına ek kart çıkaramaz.');
      },
      error: () => this.message.set('Ek kart sahibi sorgulanamadı.'),
    });
  }

  protected selectHolder(customer: Customer): void {
    if (!customer.isActive) {
      this.message.set('Pasif müşteri adına ek kart başvurusu oluşturulamaz.');
      return;
    }
    if (!customer.isProfileComplete) {
      this.message.set(`Ek kart sahibinin bilgileri tamamlanmalıdır: ${customer.missingProfileFields.join(', ')}.`);
      return;
    }
    if (!customer.birthDate || this.calculateAge(customer.birthDate) < 18) {
      this.message.set('Ek kart sahibi 18 yaşını doldurmuş olmalıdır.');
      return;
    }
    this.selectedHolder.set(customer);
    this.form.controls.supplementaryHolderCustomerId.setValue(customer.id);
    this.holderResults.set([]);
    this.summaryVisible.set(false);
  }

  protected clearHolder(): void {
    this.selectedHolder.set(null);
    this.form.controls.supplementaryHolderCustomerId.reset(null);
    this.holderQuery.reset('');
    this.summaryVisible.set(false);
  }

  protected limitExceeded(): boolean {
    const limit = this.form.controls.requestedLimit.value;
    const available = this.cardContext()?.availableSupplementaryLimit;
    return limit !== null && available !== undefined && limit > available;
  }

  protected relationshipAgeNotice(): string {
    const contextBirthDate = this.cardContext()?.primaryCustomerBirthDate;
    const holderBirthDate = this.selectedHolder()?.birthDate;
    const relationship = this.form.controls.relationship.value;
    if (!contextBirthDate || !holderBirthDate || !relationship) return '';
    const age = (value: string) => {
      const birth = new Date(`${value}T00:00:00`); const now = new Date();
      let result = now.getFullYear() - birth.getFullYear();
      if (now < new Date(now.getFullYear(), birth.getMonth(), birth.getDate())) result--;
      return result;
    };
    const primaryAge = age(contextBirthDate); const holderAge = age(holderBirthDate);
    const unusual = relationship === 'Anne' || relationship === 'Baba' ? holderAge <= primaryAge
      : relationship === 'Çocuk' ? primaryAge <= holderAge : false;
    return unusual
      ? 'Yaşlar seçilen yakınlık için olağandışı görünüyor. Evlat edinme ve üvey ebeveynlik gibi durumlar olabileceğinden başvuru engellenmez; belge kontrolü yapın.'
      : '';
  }

  protected cardStatus(value: string): string {
    return ({ Inactive: 'Pasif', Active: 'Aktif', Blocked: 'Blokeli', Expired: 'Süresi Dolmuş', Cancelled: 'Kapatılmış',
      '0': 'Pasif', '1': 'Pasif', '2': 'Aktif', '3': 'Blokeli', '4': 'Süresi Dolmuş', '5': 'Kapatılmış' } as Record<string, string>)[value] ?? value;
  }

  protected canShowSummary(): boolean {
    if (this.form.invalid || !this.cardContext() || !this.selectedHolder() || this.limitExceeded()) return false;
    const method = this.form.controls.deliveryMethod.value;
    if (method === 'RegisteredAddress') return this.deliveryAddressLabel().trim().length >= 5;
    if (method === 'Branch') return !!this.form.controls.deliveryBranch.value;
    if (this.form.controls.saveDeliveryAddress.value && this.form.controls.addressName.value.trim().length < 2) return false;
    return this.hasValidDifferentAddressSelection()
      && !!this.form.controls.deliveryBuildingNo.value.trim();
  }

  protected showSummary(): void {
    if (!this.canShowSummary()) {
      this.form.markAllAsTouched();
      this.message.set('Zorunlu alanları, limiti ve teslimat bilgilerini kontrol edin.');
      return;
    }
    this.summaryVisible.set(true);
  }

  protected create(): void {
    if (!this.canShowSummary() || this.isSubmitting()) return;
    const value = this.form.getRawValue();
    this.isSubmitting.set(true);
    this.api.createSupplementary({
      primaryCreditCardId: value.primaryCreditCardId!,
      supplementaryHolderCustomerId: value.supplementaryHolderCustomerId!,
      relationship: value.relationship,
      requestedLimit: value.requestedLimit!,
      deliveryMethod: value.deliveryMethod,
      deliveryAddress: this.deliveryAddressLabel(),
    }).subscribe({
      next: () => {
        const context = this.cardContext();
        if (value.deliveryMethod === 'DifferentAddress' && value.saveDeliveryAddress && context) {
          this.customerApi.addAddress(context.primaryCustomerId, {
            name: value.addressName.trim(), city: value.deliveryProvince, district: value.deliveryDistrict,
            neighborhood: value.deliveryNeighborhood, street: value.deliveryStreet.trim(), avenue: value.deliveryAvenue.trim() || null,
            buildingNo: value.deliveryBuildingNo.trim(), apartmentNo: value.deliveryApartmentNo.trim() || null,
            floor: value.deliveryFloor.trim() || null, postalCode: value.deliveryPostalCode.trim(), isDefault: false,
          }).subscribe({
            next: () => this.finishCreation('Ek kart başvurusu oluşturuldu; teslimat adresi de müşterinin adres defterine kaydedildi.'),
            error: response => this.finishCreation(response.error?.detail ?? 'Ek kart başvurusu oluşturuldu ancak adres defterine kaydedilemedi.'),
          });
          return;
        }
        this.finishCreation('Ek kart başvurusu oluşturuldu ve müdür kuyruğuna gönderildi.');
      },
      error: response => {
        this.isSubmitting.set(false);
        this.message.set(response.error?.detail ?? 'Ek kart başvurusu oluşturulamadı.');
      },
    });
  }

  protected resetForm(): void {
    const primaryCreditCardId = this.form.controls.primaryCreditCardId.value;
    this.form.reset({
      primaryCreditCardId,
      supplementaryHolderCustomerId: null,
      relationship: '',
      requestedLimit: null,
      deliveryMethod: 'RegisteredAddress',
      deliveryAddress: '',
      deliveryProvince: '', deliveryDistrict: '', deliveryNeighborhood: '', deliveryStreet: '', deliveryAvenue: '',
      deliveryBuildingNo: '', deliveryApartmentNo: '', deliveryFloor: '', deliveryPostalCode: '', deliveryBranch: '',
      selectedSavedAddressId: 0, addressName: '', saveDeliveryAddress: false,
    });
    this.clearHolder();
    this.summaryVisible.set(false);
  }

  protected deliveryLabel(value: DeliveryMethod): string {
    return this.deliveryOptions.find(option => option.value === value)?.label ?? value;
  }

  protected deliveryAddressLabel(): string {
    const method = this.form.controls.deliveryMethod.value;
    if (method === 'RegisteredAddress') {
      const selected = this.savedAddresses().find(x => x.id === this.form.controls.selectedSavedAddressId.value);
      return selected?.fullAddress?.trim() ?? this.cardContext()?.registeredDeliveryAddress?.trim() ?? '';
    }
    if (method === 'Branch') return this.form.controls.deliveryBranch.value;
    return this.composedDifferentAddress();
  }

  protected composedDifferentAddress(): string {
    const value = this.form.getRawValue();
    return [value.deliveryNeighborhood, value.deliveryStreet.trim(), value.deliveryAvenue.trim(),
      value.deliveryBuildingNo.trim() ? `Bina No: ${value.deliveryBuildingNo.trim()}` : '',
      value.deliveryApartmentNo.trim() ? `Daire: ${value.deliveryApartmentNo.trim()}` : '',
      value.deliveryFloor.trim() ? `Kat: ${value.deliveryFloor.trim()}` : '',
      value.deliveryPostalCode, value.deliveryDistrict && value.deliveryProvince
        ? `${value.deliveryDistrict} / ${value.deliveryProvince}` : '']
      .filter(Boolean).join(', ');
  }

  private hasValidDifferentAddressSelection(): boolean {
    const value = this.form.getRawValue();
    const provinceExists = this.provinces().some(item => item.name === value.deliveryProvince);
    const districtExists = this.districts().some(item => item.name === value.deliveryDistrict);
    const neighborhood = this.neighborhoods().find(item => item.name === value.deliveryNeighborhood);
    const streetExists = this.streets().includes(value.deliveryStreet.trim());
    const postalCodeMatches = !!neighborhood?.postalCode
      && neighborhood.postalCode === value.deliveryPostalCode.trim();
    return provinceExists && districtExists && !!neighborhood && streetExists && postalCodeMatches;
  }

  private calculateAge(birthDate: string): number {
    const birth = new Date(`${birthDate}T00:00:00`);
    if (Number.isNaN(birth.getTime())) return -1;
    const today = new Date();
    let age = today.getFullYear() - birth.getFullYear();
    if (today < new Date(today.getFullYear(), birth.getMonth(), birth.getDate())) age--;
    return age;
  }

  protected availableBranchDistricts(): string[] {
    const city = this.form.controls.deliveryProvince.value;
    return [...new Set(this.branchCatalog.filter(x => x.city === city).map(x => x.district))]
      .sort((a, b) => a.localeCompare(b, 'tr'));
  }

  protected availableBranches(): string[] {
    const city = this.form.controls.deliveryProvince.value;
    const district = this.form.controls.deliveryDistrict.value;
    return this.branchCatalog.filter(x => x.city === city && x.district === district)
      .map(x => `${x.name} — ${x.district}/${x.city}`);
  }

  protected selectSavedAddress(addressId: number): void {
    this.form.controls.selectedSavedAddressId.setValue(Number(addressId), { emitEvent: false });
    this.summaryVisible.set(false);
  }


  protected status(value: string): string {
    return ({ Pending: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' } as Record<string, string>)[value] ?? value;
  }

  private refreshContext(): void {
    const cardId = this.form.controls.primaryCreditCardId.value;
    if (!cardId) return;
    this.api.supplementaryCardContext(cardId).subscribe({
      next: context => { this.cardContext.set(context); this.loadSavedAddresses(context.primaryCustomerId); },
    });
  }

  private load(): void {
    this.api.supplementary().subscribe({
      next: data => this.applications.set(data),
      error: () => this.message.set('Ek kart kayıtları yüklenemedi.'),
    });
  }

  private resetDeliverySelection(): void {
    this.form.patchValue({ deliveryAddress: '', deliveryProvince: '', deliveryDistrict: '',
      deliveryNeighborhood: '', deliveryStreet: '', deliveryAvenue: '', deliveryBuildingNo: '', deliveryApartmentNo: '', deliveryFloor: '',
      deliveryPostalCode: '', deliveryBranch: '', selectedSavedAddressId: 0, addressName: '',
      saveDeliveryAddress: false }, { emitEvent: false });
    this.districts.set([]);
    this.neighborhoods.set([]);
    this.streets.set([]);
    this.postalCodeStatus.set('');
    this.form.controls.deliveryDistrict.disable({ emitEvent: false });
    this.form.controls.deliveryNeighborhood.disable({ emitEvent: false });
    this.form.controls.deliveryStreet.disable({ emitEvent: false });
    this.form.controls.deliveryBranch.disable({ emitEvent: false });
    if (this.form.controls.deliveryMethod.value === 'RegisteredAddress') {
      const defaultAddress = this.savedAddresses().find(item => item.isDefault) ?? this.savedAddresses()[0];
      if (defaultAddress) this.selectSavedAddress(defaultAddress.id);
    }
    this.summaryVisible.set(false);
  }

  private loadDistricts(provinceName: string): void {
    this.form.patchValue({ deliveryDistrict: '', deliveryNeighborhood: '', deliveryStreet: '', deliveryPostalCode: '', deliveryBranch: '' }, { emitEvent: false });
    this.districts.set([]);
    this.neighborhoods.set([]);
    this.streets.set([]);
    this.form.controls.deliveryDistrict.disable({ emitEvent: false });
    this.form.controls.deliveryNeighborhood.disable({ emitEvent: false });
    this.form.controls.deliveryStreet.disable({ emitEvent: false });
    this.form.controls.deliveryBranch.disable({ emitEvent: false });
    this.postalCodeStatus.set('');
    if (!provinceName) return;
    if (this.form.controls.deliveryMethod.value === 'Branch') {
      this.form.controls.deliveryDistrict.enable({ emitEvent: false });
      return;
    }
    const province = this.provinces().find(x => x.name === provinceName);
    if (!province) return;
    this.addressCatalog.getDistricts(province.id).subscribe({
      next: items => {
        this.districts.set(items);
        this.form.controls.deliveryDistrict.enable({ emitEvent: false });
      },
      error: () => this.message.set('İlçe seçim listesi yüklenemedi.'),
    });
  }

  private loadNeighborhoods(districtName: string): void {
    this.form.patchValue({ deliveryNeighborhood: '', deliveryStreet: '', deliveryPostalCode: '' }, { emitEvent: false });
    this.form.controls.deliveryBranch.setValue('', { emitEvent: false });
    this.neighborhoods.set([]);
    this.streets.set([]);
    this.form.controls.deliveryNeighborhood.disable({ emitEvent: false });
    this.form.controls.deliveryStreet.disable({ emitEvent: false });
    this.form.controls.deliveryBranch.disable({ emitEvent: false });
    this.postalCodeStatus.set('');
    if (!districtName) return;
    if (this.form.controls.deliveryMethod.value === 'Branch') {
      this.form.controls.deliveryBranch.enable({ emitEvent: false });
      return;
    }
    const district = this.districts().find(x => x.name === districtName);
    if (!district) return;
    this.addressCatalog.getNeighborhoods(district.id).subscribe({
      next: items => {
        this.neighborhoods.set(items);
        this.form.controls.deliveryNeighborhood.enable({ emitEvent: false });
      },
      error: () => this.message.set('Mahalle seçim listesi yüklenemedi.'),
    });
  }

  private loadNeighborhoodAddressData(neighborhoodName: string): void {
    this.form.patchValue({ deliveryStreet: '', deliveryPostalCode: '' }, { emitEvent: false });
    this.streets.set([]);
    this.form.controls.deliveryStreet.disable({ emitEvent: false });
    this.postalCodeStatus.set('');
    if (!neighborhoodName || this.form.controls.deliveryMethod.value !== 'DifferentAddress') return;
    const neighborhood = this.neighborhoods().find(item => item.name === neighborhoodName);
    if (!neighborhood) return;
    this.form.controls.deliveryPostalCode.setValue(neighborhood.postalCode ?? '', { emitEvent: false });
    this.postalCodeStatus.set(neighborhood.postalCodeStatus ?? 'estimated');
    this.addressCatalog.getStreets(neighborhood.id).subscribe({
      next: items => {
        this.streets.set(items.map(item => item.name));
        this.form.controls.deliveryStreet.enable({ emitEvent: false });
      },
      error: () => this.message.set('Ulusal sokak/cadde kataloğu yüklenemedi.'),
    });
  }

  private loadSavedAddresses(customerId: number): void {
    this.customerApi.getAddresses(customerId).subscribe({
      next: addresses => {
        this.savedAddresses.set(addresses);
        this.form.controls.selectedSavedAddressId.setValue(
          (addresses.find(x => x.isDefault) ?? addresses[0])?.id ?? 0,
          { emitEvent: false },
        );
      },
      error: () => this.savedAddresses.set([]),
    });
  }

  private finishCreation(message: string): void {
    this.message.set(message);
    this.isSubmitting.set(false);
    this.resetForm();
    this.refreshContext();
    this.load();
  }
}
