import { Component, OnInit, computed, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CustomerApiService } from '../customer-api.service';
import { Customer } from '../customer.models';

type CustomerStatusFilter = 'All' | 'Active' | 'Passive';
type ProfileFilter = 'All' | 'Complete' | 'Missing';
type SortOrder = 'NameAsc' | 'NameDesc' | 'NumberAsc';

@Component({
  selector: 'app-customer-list',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="page">
      <header>
        <div><p>MÜŞTERİ İŞLEMLERİ</p><h1>Müşteriler</h1><span>Yetkiniz kapsamındaki kayıtları maskeli bilgilerle görüntüleyin; tam bilgiler yalnızca müşteri detayında açılır.</span></div>
      </header>

      <div class="summary">
        <div><span>TOPLAM MÜŞTERİ</span><b>{{ customers().length }}</b></div>
        <div><span>AKTİF</span><b>{{ activeCount() }}</b></div>
        <div><span>PASİF</span><b>{{ customers().length - activeCount() }}</b></div>
        <div><span>EKSİK BİLGİLİ</span><b>{{ incompleteCount() }}</b></div>
      </div>

      <article>
        <div class="filters">
          <label class="search">Arama<input [formControl]="query" placeholder="Ad soyad, müşteri no, TC, telefon veya e-posta"></label>
          <label>Durum<select [formControl]="status"><option value="All">Tüm durumlar</option><option value="Active">Aktif</option><option value="Passive">Pasif</option></select></label>
          <label>Bilgi Durumu<select [formControl]="profile"><option value="All">Tümü</option><option value="Complete">Bilgileri tam</option><option value="Missing">Bilgileri eksik</option></select></label>
          <label>Sıralama<select [formControl]="sort"><option value="NameAsc">Ad A–Z</option><option value="NameDesc">Ad Z–A</option><option value="NumberAsc">Müşteri no</option></select></label>
          @if (hasFilters()) { <button type="button" (click)="clearFilters()">Filtreleri Temizle</button> }
        </div>

        @if (error()) { <p class="error">{{ error() }} <button type="button" (click)="error.set('')" aria-label="Uyarıyı kapat">×</button></p> }
        <div class="table-wrap"><table>
          <thead><tr><th>MÜŞTERİ NO</th><th>AD SOYAD</th><th>TC KİMLİK NO</th><th>İLETİŞİM</th><th>KONUM</th><th>BİLGİ DURUMU</th><th>DURUM</th><th>İŞLEM</th></tr></thead>
          <tbody>
            @for (customer of filteredCustomers(); track customer.id) {
              <tr>
                <td><b>{{ customer.customerNumber }}</b></td>
                <td>{{ customer.firstName }} {{ customer.lastName }}</td>
                <td>{{ maskIdentity(customer.nationalIdentityNumber) }}</td>
                <td><span>{{ maskPhone(customer.phoneCountryCode, customer.phoneNumber) }}</span><small>{{ maskEmail(customer.emailAddress) }}</small></td>
                <td>{{ customer.city || '—' }}{{ customer.district ? ' / ' + customer.district : '' }}</td>
                <td><span class="badge" [class.warning]="!customer.isProfileComplete">{{ customer.isProfileComplete ? 'Tam' : 'Eksik' }}</span></td>
                <td><span class="badge" [class.passive]="!customer.isActive">{{ customer.isActive ? 'Aktif' : 'Pasif' }}</span></td>
                <td><a [routerLink]="['/officer/customers', customer.id]">Detayı İncele →</a></td>
              </tr>
            } @empty { <tr><td colspan="8" class="empty">Filtrelere uygun müşteri bulunamadı.</td></tr> }
          </tbody>
        </table></div>
      </article>
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1440px;margin:0 auto;padding:34px;color:#20364d}header{display:flex;justify-content:space-between;align-items:end;margin-bottom:22px}header p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.16em}h1{margin:7px 0 4px;color:#0b2a4d;font-size:32px}header span{color:#718093}header a,.filters button{padding:11px 15px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800;text-decoration:none}.summary{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:18px}.summary div{display:grid;gap:8px;padding:17px;border:1px solid #dce3e9;border-radius:10px;background:#fff}.summary span{color:#7a8999;font-size:9px;font-weight:800}.summary b{color:#0b2a4d;font-size:24px}article{padding:18px;border:1px solid #dce3e9;border-radius:11px;background:#fff}.filters{display:grid;grid-template-columns:minmax(260px,2fr) repeat(3,minmax(150px,1fr)) auto;gap:10px;align-items:end;margin-bottom:17px}.filters label{display:grid;gap:6px;font-size:11px;font-weight:800}.filters input,.filters select{height:43px;padding:0 11px;border:1px solid #ccd6df;border-radius:7px;background:#fff;color:#20364d}.filters button{height:43px;cursor:pointer}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:14px 12px;border-bottom:1px solid #edf1f4;text-align:left;white-space:nowrap;font-size:12px}th{color:#7a8999;background:#f8fafb;font-size:9px;letter-spacing:.04em}td small{display:block;margin-top:4px;color:#7a8999}td a{color:#0d4777;font-weight:800;text-decoration:none}.badge{display:inline-flex;padding:5px 8px;border-radius:999px;color:#17734d;background:#e7f6ef;font-size:10px;font-weight:800}.badge.warning{color:#8a6410;background:#fff3d8}.badge.passive{color:#9e3333;background:#fdebea}.empty{padding:40px;text-align:center;color:#7a8999}.error{display:flex;justify-content:space-between;padding:12px;border-radius:7px;color:#a52b2b;background:#fdecec}.error button{border:0;color:inherit;background:transparent;font-size:19px}@media(max-width:1050px){.filters{grid-template-columns:1fr 1fr}.summary{grid-template-columns:1fr 1fr}}@media(max-width:650px){.page{padding:20px}header{align-items:flex-start;flex-direction:column;gap:14px}.filters,.summary{grid-template-columns:1fr}}
  `,
})
export class CustomerList implements OnInit {
  protected readonly customers = signal<Customer[]>([]);
  protected readonly error = signal('');
  protected readonly query = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<CustomerStatusFilter>('All', { nonNullable: true });
  protected readonly profile = new FormControl<ProfileFilter>('All', { nonNullable: true });
  protected readonly sort = new FormControl<SortOrder>('NameAsc', { nonNullable: true });
  private readonly filterVersion = signal(0);
  protected readonly activeCount = computed(() => this.customers().filter(x => x.isActive).length);
  protected readonly incompleteCount = computed(() => this.customers().filter(x => !x.isProfileComplete).length);
  protected readonly filteredCustomers = computed(() => {
    this.filterVersion();
    const search = this.query.value.trim().toLocaleLowerCase('tr-TR');
    const rows = this.customers().filter(customer => {
      const searchable = `${customer.customerNumber} ${customer.nationalIdentityNumber} ${customer.firstName} ${customer.lastName} ${customer.phoneCountryCode}${customer.phoneNumber} ${customer.emailAddress}`.toLocaleLowerCase('tr-TR');
      const statusMatches = this.status.value === 'All' || (this.status.value === 'Active' ? customer.isActive : !customer.isActive);
      const profileMatches = this.profile.value === 'All' || (this.profile.value === 'Complete' ? customer.isProfileComplete : !customer.isProfileComplete);
      return (!search || searchable.includes(search)) && statusMatches && profileMatches;
    });
    return rows.sort((a, b) => this.sort.value === 'NumberAsc'
      ? a.customerNumber.localeCompare(b.customerNumber, 'tr')
      : this.sort.value === 'NameDesc'
        ? `${b.firstName} ${b.lastName}`.localeCompare(`${a.firstName} ${a.lastName}`, 'tr')
        : `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`, 'tr'));
  });
  protected readonly hasFilters = computed(() => {
    this.filterVersion();
    return !!this.query.value || this.status.value !== 'All' || this.profile.value !== 'All' || this.sort.value !== 'NameAsc';
  });

  constructor(private readonly api: CustomerApiService) {
    [this.query, this.status, this.profile, this.sort].forEach(control =>
      control.valueChanges.subscribe(() => this.filterVersion.update(value => value + 1)));
  }
  ngOnInit(): void { this.api.getAll().subscribe({ next: rows => this.customers.set(rows), error: () => this.error.set('Müşteri listesi yüklenemedi.') }); }
  protected clearFilters(): void { this.query.reset(''); this.status.reset('All'); this.profile.reset('All'); this.sort.reset('NameAsc'); }
  protected maskIdentity(value: string): string { return value.length < 4 ? '***' : `${value.slice(0, 2)}*******${value.slice(-2)}`; }
  protected maskPhone(code: string, value: string): string { return `${code} ${value.slice(0, 3)} *** ** ${value.slice(-2)}`; }
  protected maskEmail(value: string): string {
    const [name, domain] = value.split('@');
    return domain ? `${name.slice(0, 2)}***@${domain}` : '***';
  }
}
