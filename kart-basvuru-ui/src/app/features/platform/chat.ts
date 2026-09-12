import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { interval, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { ChatMessage, ChatUser, PlatformApiService } from '../../core/services/platform-api.service';

@Component({
  selector: 'app-chat',
  imports: [ReactiveFormsModule, DatePipe],
  template: `
    <section class="page"><p>PERSONEL İLETİŞİMİ</p><div class="title-row"><h1>Personel İletişim Merkezi</h1><div class="title-actions"><button type="button" (click)="prepareMeeting()">Görüşme Bağlantısı Hazırla</button><button type="button" (click)="toggleBulkMode()">{{ bulkMode() ? 'Bireysel Sohbet' : 'Toplu Mesaj / CC' }}</button></div></div>
      @if(errorMessage()){<div class="error">{{ errorMessage() }}<button type="button" (click)="errorMessage.set('')">×</button></div>}
      @if(successMessage()){<div class="success">{{ successMessage() }}<button type="button" (click)="successMessage.set('')">×</button></div>}
      <div class="chat">
        <aside><h2>{{ bulkMode() ? 'Alıcılar' : 'Kişiler' }}</h2>@for (user of users(); track user.id) {
          @if(bulkMode()){<div class="bulk-user"><span><b>{{ user.fullName }}</b><small>{{ user.role === 'Manager' ? 'Müdür' : 'Memur' }}</small></span><label><input type="checkbox" [checked]="recipientIds().includes(user.id)" (change)="toggleRecipient(user.id, false)"> Alıcı</label><label><input type="checkbox" [checked]="ccRecipientIds().includes(user.id)" (change)="toggleRecipient(user.id, true)"> CC</label></div>}
          @else {<button [class.active]="selectedUser()?.id===user.id" (click)="select(user)"><i>{{ user.fullName.slice(0,2) }}</i><span><b>{{ user.fullName }}</b><small>{{ user.role === 'Manager' ? 'Müdür' : 'Memur' }}</small></span></button>}
        }</aside>
        <article>
          @if (bulkMode()) {
            <header><b>Toplu kurum içi mesaj</b><span>{{ recipientIds().length }} alıcı · {{ ccRecipientIds().length }} bilgi (CC)</span></header>
            <div class="bulk-compose"><h2>Mesaj Alıcıları</h2><p><b>Alıcı:</b> {{ selectedNames(recipientIds()) || 'Seçilmedi' }}</p><p><b>CC:</b> {{ selectedNames(ccRecipientIds()) || 'Seçilmedi' }}</p><label>İlgili başvuru ID (isteğe bağlı)<input type="number" min="1" [formControl]="applicationId" placeholder="Örn. 125"></label><p class="policy">Sunucu; hakaret/taciz, parola veya doğrulama kodu isteme, tam kart bilgisi talebi ve şüpheli yönlendirmeleri engeller. Tekrarlanan mesajlarda spam koruması uygulanır.</p></div>
            <form (submit)="$event.preventDefault(); sendBulk()"><div class="message-field"><input [formControl]="message" placeholder="Toplu mesajınızı yazın..." maxlength="1000">@if(forbiddenMessage()){<small>Mesaj, kurum içi iletişim kurallarına aykırı ifade içerdiği için gönderilemez.</small>}</div><button [disabled]="message.invalid || recipientIds().length + ccRecipientIds().length === 0 || !!forbiddenMessage()">Gönder</button></form>
          } @else if (selectedUser(); as user) {
            <header><b>{{ user.fullName }}</b><span>{{ user.role === 'Manager' ? 'Müdür' : 'Memur' }} · Kurum içi güvenli mesaj</span></header>
            <div class="messages">@for (item of messages(); track item.id) { <div [class.mine]="item.senderUserId===currentUserId()"><b>{{ item.senderName }}</b><p>{{ item.message }}</p><small>{{ item.createdAtUtc | date:'dd.MM HH:mm' }}</small>@if(item.senderUserId!==currentUserId()){<span class="report-actions"><button type="button" (click)="report(item, 'Spam')">Spam bildir</button><button type="button" (click)="report(item, 'Uygunsuz İçerik')">Uygunsuz bildir</button></span>}</div> } @empty { <p class="empty">Henüz mesaj yok.</p> }</div>
            <form (submit)="$event.preventDefault(); send()"><div class="message-field"><input [formControl]="message" placeholder="Mesajınızı yazın..." maxlength="1000">@if(forbiddenMessage()){<small>Mesaj, kurum içi iletişim kurallarına aykırı ifade içerdiği için gönderilemez.</small>}</div><button [disabled]="message.invalid || !!forbiddenMessage()">Gönder</button></form>
          } @else { <div class="choose">Sohbet başlatmak için bir personel seçin.</div> }
        </article>
      </div>
    </section>
  `,
  styles: `
    :host{display:block}.page{max-width:1100px;margin:auto;padding:30px;color:#20364d}.page>p{margin:0;color:#a17c22;font-size:10px;font-weight:900;letter-spacing:.16em}.title-row{display:flex;align-items:center;justify-content:space-between}.title-row h1{margin:7px 0 20px;color:#0b2a4d}.title-row button{padding:9px 12px;border:1px solid #174d78;border-radius:7px;color:#174d78;background:#fff;font-weight:800}.error,.success{display:flex;justify-content:space-between;margin-bottom:12px;padding:11px;border-radius:8px}.error{color:#a52b2b;background:#fdecec}.success{color:#166b4b;background:#e5f5ed}.error button,.success button{border:0;color:inherit;background:transparent;font-size:20px}.chat{height:640px;display:grid;grid-template-columns:280px 1fr;border:1px solid #dce3e9;border-radius:12px;background:#fff;overflow:hidden}.chat aside{padding:16px;border-right:1px solid #e3e8ed;overflow:auto}.chat h2{font-size:15px}.chat aside>button{width:100%;padding:10px;display:flex;gap:10px;align-items:center;border:0;border-radius:8px;background:#fff;text-align:left;cursor:pointer}.chat aside>button.active{background:#edf3f8}.chat aside i{width:36px;height:36px;display:grid;place-items:center;border-radius:50%;color:#fff;background:#174d78;font-style:normal}.chat aside span,.chat aside b,.chat aside small{display:block}.chat aside small{color:#7d8a98}.bulk-user{display:grid;grid-template-columns:1fr auto;gap:4px 8px;padding:10px;border-bottom:1px solid #edf1f4}.bulk-user span{grid-row:span 2}.bulk-user label{display:flex;align-items:center;gap:4px;font-size:11px}.chat article{display:grid;grid-template-rows:auto 1fr auto;min-width:0}.chat header{height:auto;padding:16px;border-bottom:1px solid #e3e8ed;display:grid;background:#fff}.chat header span{margin-top:3px}.messages{padding:20px;overflow:auto;background:#f6f8fa}.messages>div{width:max-content;max-width:70%;margin:8px 0;padding:10px 13px;border-radius:10px;background:#fff;box-shadow:0 2px 8px rgba(20,45,70,.07)}.messages>div.mine{margin-left:auto;color:#fff;background:#174d78}.messages p{margin:5px 0}.messages small{opacity:.7}.bulk-compose{padding:24px;background:#f6f8fa}.bulk-compose label{display:grid;gap:6px;max-width:300px;font-weight:800}.bulk-compose input{padding:10px;border:1px solid #cad5df;border-radius:7px}.policy{padding:10px;border-left:3px solid #b88918;color:#6f5b2e;background:#fff9e9}.chat form{padding:14px;display:flex;align-items:flex-start;gap:9px;border-top:1px solid #e3e8ed}.message-field{display:grid;flex:1;gap:5px}.message-field input{padding:11px;border:1px solid #cad5df;border-radius:7px}.message-field small{color:#a52b2b}.chat form button{padding:10px 16px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}.chat form button:disabled{opacity:.5}.choose{grid-row:1/-1;min-height:100%;display:grid;place-items:center;padding:24px;color:#718093;text-align:center}.empty{text-align:center;color:#718093}@media(max-width:720px){.page{padding:20px}.title-row{align-items:flex-start;flex-direction:column}.chat{grid-template-columns:120px 1fr}.chat aside>button span{display:none}}
  `,
})
export class Chat implements OnInit {
  protected readonly users = signal<ChatUser[]>([]);
  protected readonly selectedUser = signal<ChatUser | null>(null);
  protected readonly messages = signal<ChatMessage[]>([]);
  protected readonly message = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  protected readonly applicationId = new FormControl<number | null>(null);
  protected readonly bulkMode = signal(false);
  protected readonly recipientIds = signal<number[]>([]);
  protected readonly ccRecipientIds = signal<number[]>([]);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  constructor(private readonly api: PlatformApiService, private readonly auth: AuthService, private readonly route: ActivatedRoute, destroyRef: DestroyRef) {
    interval(5000).pipe(startWith(0), switchMap(() => {
      const selected = this.selectedUser();
      return selected ? this.api.conversation(selected.id) : this.api.chatUsers();
    }), takeUntilDestroyed(destroyRef)).subscribe(data => {
      if (this.selectedUser()) this.messages.set(data as ChatMessage[]); else this.users.set(data as ChatUser[]);
    });
  }
  ngOnInit() {
    this.api.chatUsers().subscribe(users => {
      this.users.set(users);
      const id = Number(this.route.snapshot.queryParamMap.get('user'));
      const selected = users.find(x => x.id === id);
      if (selected) this.select(selected);
    });
  }
  protected select(user: ChatUser) { this.selectedUser.set(user); this.errorMessage.set(''); this.successMessage.set(''); this.api.conversation(user.id).subscribe(data => this.messages.set(data)); }
  protected send() {
    const user = this.selectedUser(); if (!user || this.message.invalid || this.forbiddenMessage()) return;
    this.api.sendMessage(user.id, this.message.value.trim()).subscribe({ next: () => { this.message.reset(); this.api.conversation(user.id).subscribe(data => this.messages.set(data)); }, error: response => this.errorMessage.set(response.error?.detail ?? 'Mesaj gönderilemedi.') });
  }
  protected toggleBulkMode() { this.bulkMode.update(value => !value); this.message.reset(''); this.errorMessage.set(''); this.successMessage.set(''); }
  protected prepareMeeting() {
    this.message.setValue('Görüntülü görüşme talebi: Uygun olduğunuz saat aralığını paylaşır mısınız?');
    this.successMessage.set('Görüşme talebi hazırlandı. Bankacılık ortamında dış bağlantı üretmek yerine kurumsal Teams/WebRTC sağlayıcısı bağlandığında bağlantı sunucu tarafından oluşturulmalıdır.');
  }
  protected toggleRecipient(id: number, cc: boolean) {
    const target = cc ? this.ccRecipientIds : this.recipientIds;
    const other = cc ? this.recipientIds : this.ccRecipientIds;
    target.update(ids => ids.includes(id) ? ids.filter(item => item !== id) : [...ids, id]);
    other.update(ids => ids.filter(item => item !== id));
  }
  protected selectedNames(ids: number[]): string { return this.users().filter(user => ids.includes(user.id)).map(user => user.fullName).join(', '); }
  protected forbiddenMessage(): string {
    const normalized = this.message.value.toLocaleLowerCase('tr-TR');
    return ['aptal', 'salak', 'gerizekalı', 'hakaret', 'küfür', 'şifreni gönder', 'parolanı gönder',
      'sms kodunu gönder', 'doğrulama kodunu gönder', 'cvv gönder', 'kart numarasının tamamı',
      'kart şifreni yaz', 'hemen para gönder', 'ödül kazandın tıkla', 'hesabın kapanacak tıkla']
      .find(word => normalized.includes(word)) ?? '';
  }
  protected report(item: ChatMessage, reason: 'Spam' | 'Uygunsuz İçerik') {
    this.api.reportMessage(item.id, reason).subscribe({
      next: () => { this.errorMessage.set(''); this.successMessage.set(`Mesaj “${reason}” nedeniyle bildirildi ve denetim kaydına alındı.`); },
      error: response => this.errorMessage.set(response.error?.detail ?? 'Mesaj bildirilemedi.'),
    });
  }
  protected sendBulk() {
    if (this.message.invalid || this.forbiddenMessage() || this.recipientIds().length + this.ccRecipientIds().length === 0) return;
    this.api.sendBulkMessage(this.recipientIds(), this.ccRecipientIds(), this.message.value.trim(), this.applicationId.value).subscribe({
      next: () => { this.message.reset(''); this.applicationId.reset(null); this.recipientIds.set([]); this.ccRecipientIds.set([]); this.errorMessage.set(''); this.successMessage.set('Toplu mesaj alıcılara kaydedildi ve iletildi.'); },
      error: response => this.errorMessage.set(response.error?.detail ?? 'Toplu mesaj gönderilemedi.'),
    });
  }
  protected currentUserId() { return this.auth.currentUser()?.id ?? 0; }
}
