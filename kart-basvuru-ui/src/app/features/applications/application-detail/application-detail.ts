import { CurrencyPipe, DatePipe, Location } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CardApplicationApiService } from '../card-application-api.service';
import { ApplicationDocument, CardApplicationDetail, CardType } from '../card-application.models';
import { Fulfillment, KycAssessment, PlatformApiService, PreAssessment, RevisionComparison } from '../../../core/services/platform-api.service';

type Decision = 'Approved' | 'Rejected' | 'Revision';
type DeliveryMethod = 'RegisteredAddress' | 'DifferentAddress' | 'Branch';
type StatementPreference = 'Email' | 'Paper' | 'Mobile';

@Component({
  selector: 'app-application-detail',
  imports: [CurrencyPipe, DatePipe, RouterLink, ReactiveFormsModule],
  styleUrl: './application-detail-policy.scss',
  template: `
    <section class="page">
      @if (error()) { <div class="error" role="alert"><span>{{ error() }}</span><button type="button" (click)="error.set('')" aria-label="Uyarıyı kapat">×</button></div> }
      @if (notice()) { <div class="toast" role="status"><span>{{ notice() }}</span><button type="button" (click)="notice.set('')" aria-label="Bildirimi kapat">×</button></div> }
      @if (detail(); as item) {
        <header class="heading">
          <div><p>BAŞVURU</p><h1>Başvuru Detayı</h1><span>Başvuru, müşteri, teslimat ve kart tercihlerini tek ekranda inceleyin.</span></div>
          <div class="header-actions">
            <button type="button" (click)="goBack()">← Geldiğim Sayfaya Dön</button>
            <button type="button" (click)="downloadPdf(false)">PDF İndir</button>
            <button type="button" (click)="downloadPdf(true)">PDF'i E-postala</button>
            @if (item.creditCardId) { <a [routerLink]="cardRoute(item.creditCardId)">Oluşturulan Kartı Görüntüle</a> }
            @if (item.application.status === 'Pending' || item.application.status === 'Revision') {
              <button type="button" class="danger-action" (click)="openCancellation()">{{ isManager() ? 'Operasyonel İptal' : 'Başvuruyu Geri Çek' }}</button>
            }
            @if (isManager() && item.application.status === 'Pending') {
              <button type="button" (click)="startDecision('Approved')" [disabled]="item.application.isEvaluationBlocked || isFirstApprover(item)">
                {{ item.application.isEvaluationBlocked ? 'Kuyrukta Bekliyor' : isFirstApprover(item) ? 'Bağımsız Müdür Onayı Gerekli' : item.application.workflowStage === 'SecondManagerApproval' ? 'İkinci Onayı Değerlendir' : 'Değerlendirmeye Git' }}
              </button>
            }
          </div>

        </header>

        @if (isManager() && (item.application.requiresSecondApproval || item.application.requestedLimit > 100000 || item.application.preAssessmentRiskLevel === 'HIGH')) {
          <aside class="workflow-alert"><b>{{ item.application.workflowStage === 'SecondManagerApproval' ? 'İkinci Müdür Onayı Bekleniyor' : 'Çift Kontrol Uygulanacak' }}</b><span>{{ item.application.approvalPolicy }}</span>@if(item.application.firstApproverName){<small>Birinci onay: {{ item.application.firstApproverName }} · {{ item.application.firstApprovedAtUtc | date:'dd.MM.yyyy HH:mm' }}</small>}</aside>
        }
        @if (isManager() && item.application.isEvaluationBlocked) {
          <aside class="workflow-alert"><b>Önceki başvuru bekleniyor</b><span>Aynı müşterinin daha önce oluşturulmuş açık başvurusu sonuçlanmadan bu kayıt değerlendirilemez. Bu kural toplam kart limitinin başvuru sırasına göre yeniden hesaplanmasını sağlar.</span><small>Önce müşterinin başvuru geçmişindeki en eski açık kayıt sonuçlandırılmalıdır.</small></aside>
        }

        @if (isManager() && preAssessment(); as assessment) {
          <section class="assessment"><div class="assessment-title"><div><p>KARAR DESTEK</p><h2>Açıklanabilir Ön Değerlendirme</h2></div><small>Bu puan otomatik karar vermez; nihai karar müdüre aittir.</small></div><div class="assessment-score"><b>{{ assessment.score }}/100</b><span>{{ riskLabel(assessment.riskLevel) }} · {{ recommendationLabel(assessment.recommendation) }}</span></div>
            <div class="factor-grid"><div><h3>Olumlu Faktörler</h3><ul>@for(factor of assessment.positiveFactors;track factor){<li>✓ {{factor}}</li>}</ul></div><div><h3>Risk Faktörleri</h3><ul>@for(factor of assessment.riskFactors;track factor){<li>! {{factor}}</li>}</ul></div></div><small>{{ assessment.disclaimer }}</small>
          </section>
        }

        @if (kycAssessment(); as kyc) {
          <section class="kyc" [class.blocked]="kyc.overallStatus === 'Blocked'">
            <div class="assessment-title"><div><p>KYC / MÜŞTERİ UYGUNLUĞU</p><h2>{{ kycStatusLabel(kyc.overallStatus) }}</h2></div><small>{{ kyc.checkedAtUtc | date:'dd.MM.yyyy HH:mm' }}</small></div>
            <div class="kyc-grid">@for(check of kyc.checks;track check.key){<div [class.failed]="check.status === 'Failed'" [class.review]="check.status === 'Review'"><b>{{ check.status === 'Passed' ? '✓' : check.status === 'Review' ? '!' : '×' }} {{check.label}}</b><span>{{check.detail}}</span></div>}</div>
            <small>{{kyc.disclaimer}}</small>
          </section>
        }

        <article class="content-card">
          <div class="application-strip">
            <div><span>BAŞVURU NO</span><b>{{ item.application.applicationNumber }} <button type="button" class="copy-button" (click)="copyApplicationNumber(item.application.applicationNumber)" title="Başvuru numarasını kopyala">⧉</button></b></div>
            <div><span>BAŞVURU TARİHİ</span><b>{{ item.application.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</b></div>
            <div><span>BAŞVURU DURUMU</span><b [class]="statusClass(item.application.status)">● {{ statusLabel(item.application.status) }}</b></div>
          </div>

          <section><h2>Başvuru ve Kart Süreci</h2>
            <ol class="process-timeline">
              @for (step of processSteps(item); track step.key) {
                <li [class]="step.state">
                  <i>{{ step.state === 'completed' ? '✓' : step.state === 'rejected' ? '×' : step.state === 'active' ? '●' : '○' }}</i>
                  <div><b>{{ step.label }}</b><span>{{ step.description }}</span>@if (step.date) { <small>{{ step.date | date:'dd.MM.yyyy HH:mm' }}</small> }</div>
                </li>
              }
            </ol>
          </section>

          <section><h2>Müşteri Bilgileri</h2><div class="info-grid">
            <div><span>Ad Soyad</span>
              @if (isOfficer()) { <a [routerLink]="['/officer/customers', item.application.customerId]">{{ item.application.customerFullName }} →</a> }
              @else { <a [routerLink]="['/manager/customers', item.application.customerId]">{{ item.application.customerFullName }} →</a> }
            </div>
            <div><span>Müşteri No</span><b>{{ item.application.customerNumber }}</b></div>
            <div><span>TC Kimlik No</span><b>{{ item.application.customerNationalIdentityNumber }}</b></div>
            <div><span>Telefon</span><b>{{ item.application.customerPhoneCountryCode }} {{ item.application.customerPhoneNumber }}</b></div>
            <div><span>E-posta</span><b>{{ item.application.customerEmailAddress }}</b></div>
            <div><span>Kredi Notu</span><button type="button" class="detail-link" (click)="showCreditScoreExplanation(item.application.customerCreditScore)">{{ item.application.customerCreditScore || 'Belirtilmedi' }} · Detayı</button></div>
          </div></section>

          <section><h2>Kart ve Limit Bilgileri</h2><div class="info-grid">
            <div><span>Kart Tipi</span><b>{{ item.application.cardTypeName }}</b></div>
            <div><span>Hesaplanan Azami Limit</span><button type="button" class="detail-link" (click)="showLimitExplanation()">{{ item.application.availableLimit | currency:'TRY':'symbol-narrow':'1.0-0' }} · Hesaplama</button></div>
            <div><span>Talep Edilen Limit</span><b>{{ item.application.requestedLimit | currency:'TRY':'symbol-narrow':'1.0-0' }}</b></div>
            <div><span>Onaylanan Limit</span><b>{{ item.approvedLimit ? (item.approvedLimit | currency:'TRY':'symbol-narrow':'1.0-0') : 'Henüz belirlenmedi' }}</b></div>
          </div></section>

          <section><h2>Kart Teslimat Bilgileri</h2><div class="info-grid">
            <div><span>Teslimat Yöntemi</span><b>{{ deliveryLabel(item.application.deliveryMethod) }}</b></div>
            <div><span>Teslimat Durumu</span><b>{{ item.creditCardId ? 'Üretim aşamasında' : 'Henüz başlamadı' }}</b></div>
            <div class="wide"><span>Teslimat Adresi / Şube</span><b>{{ item.application.deliveryBranch || item.application.deliveryAddress }}</b></div>
          </div></section>

          <section><h2>Kart Kullanım Tercihleri</h2><div class="info-grid">
            <div><span>Ekstre Gönderim Tercihi</span><b>{{ statementLabel(item.application.statementPreference) }}</b></div>
            <div><span>Hesap Kesim / Son Ödeme</span><b>Ayın {{ item.application.statementDay }}. günü / {{ paymentDueLabel(item.application.statementDay) }}</b></div>
            <div><span>Düzenli Limit Artışı</span><b>{{ item.application.automaticLimitIncreaseEnabled ? 'Teklif izni açık' : 'Kapalı' }}</b></div>
            <div><span>Temassız Kullanım</span><b>{{ item.application.contactlessEnabled ? 'Açık' : 'Kapalı' }}</b></div>
            <div><span>İnternet Alışverişi</span><b>{{ item.application.internetShoppingEnabled ? 'Açık' : 'Kapalı' }}</b></div>
          </div></section>

          <section><h2>Belge Kontrolü</h2><div class="info-grid">
            <div><span>Kimlik Belgesi</span><b>{{ item.application.identityDocumentConfirmed ? '✓ Kontrol edildi' : 'Eksik' }}</b></div>
            <div><span>Gelir Belgesi</span><b>{{ item.application.incomeDocumentConfirmed ? '✓ Kontrol edildi' : 'Eksik' }}</b></div>
            <div><span>İkametgah</span><b>{{ item.application.residenceDocumentConfirmed ? '✓ Kontrol edildi' : 'Eksik' }}</b></div>
          </div></section>

          <section><h2>Yüklenen Belgeler ve İsteğe Bağlı Kalite Kontrolü</h2><p class="document-policy-note">Kimlik, gelir ve ikamet belgesinin sisteme yüklenmesi onay için yeterlidir. Müdür doğrulaması zorunlu bir kapı değildir; yalnızca okunabilirlik veya içerik şüphesinde kalite kontrolü amacıyla kullanılabilir.</p><div class="document-list">
            @for(document of documents();track document.id){<div class="document-item"><button type="button" (click)="downloadDocument(document)"><b>{{ documentTypeLabel(document.documentType) }}</b><span>{{ document.fileName }}</span><small>{{ document.uploadedAtUtc | date:'dd.MM.yyyy HH:mm' }} · SHA-256: {{ document.sha256.slice(0,12) }}…</small></button><em [class]="'doc-' + document.verificationStatus.toLowerCase()">{{ documentVerificationLabel(document.verificationStatus) }}</em>@if(isManager() && document.verificationStatus !== 'Verified'){<button type="button" class="verify" (click)="verifyDocument(document)">Doğrula</button><button type="button" class="reject" (click)="openDocumentRejection(document)">Reddet</button>}</div>}
            @empty { <p>Bu başvuruya yüklenmiş dosya bulunmuyor.</p> }
          </div></section>

          <section><h2>Başvuru Açıklamaları</h2><div class="info-grid">
            <div><span>Başvuru Notu</span><b>{{ item.application.applicationNote || '—' }}</b></div>
            @if(item.evaluationNote){<div><span>Müdür Değerlendirme Notu</span><b>{{ item.evaluationNote }}</b></div>}
          </div></section>

          @if (item.application.status === 'Rejected') {
            <section class="rejection-detail"><h2>Red Bilgileri</h2><div class="info-grid">
              <div><span>Red Nedeni</span><b>{{ decisionReason(item.evaluationNote) }}</b></div>
              <div class="wide"><span>Açıklama</span><b>{{ decisionExplanation(item.evaluationNote) }}</b></div>
            </div></section>
          }

          @if (isOfficer() && item.application.status === 'Revision') {
            <section class="revision-panel">
              <div class="revision-note"><b>Revizyon Notu</b><p>{{ item.evaluationNote }}</p></div>
              <h2>Revizyonlu Başvuruyu Güncelle</h2>
              <form [formGroup]="revisionForm" (ngSubmit)="resubmit()">
                <div class="form-grid">
                  <label>Kart Tipi<select formControlName="cardTypeId">@for (type of cardTypes(); track type.id) { <option [value]="type.id">{{ type.name }}</option> }</select></label>
                  <label>Talep Edilen Limit<input type="number" formControlName="requestedLimit"></label>
                  <label>Teslimat Yöntemi<select formControlName="deliveryMethod"><option value="RegisteredAddress">Kayıtlı Adrese Teslim</option><option value="DifferentAddress">Farklı Adrese Teslim</option><option value="Branch">Şubeden Teslim</option></select></label>
                  <label>Ekstre Tercihi<select formControlName="statementPreference"><option value="Email">E-posta</option><option value="Paper">Basılı Ekstre</option><option value="Mobile">Mobil Bildirim</option></select></label>
                  <label>Ekstre Kesim Günü<select formControlName="statementDay"><option [ngValue]="7">Ayın 7'si</option><option [ngValue]="14">Ayın 14'ü</option><option [ngValue]="21">Ayın 21'i</option><option [ngValue]="28">Ayın 28'i</option></select></label>
                  <label class="wide">Teslimat Adresi / Şube<input formControlName="deliveryAddress"></label>
                  <label class="wide">Başvuru Notu<textarea rows="3" formControlName="applicationNote"></textarea></label>
                  <label class="check"><input type="checkbox" formControlName="contactlessEnabled"> Temassız Kullanım</label>
                  <label class="check"><input type="checkbox" formControlName="internetShoppingEnabled"> İnternet Alışverişi</label>
                  <label class="check"><input type="checkbox" formControlName="automaticLimitIncreaseEnabled"> Düzenli Limit Artışı Teklif İzni</label>
                  <label class="check"><input type="checkbox" formControlName="identityDocumentConfirmed"> Kimlik belgesi kontrol edildi</label>
                  <label class="check"><input type="checkbox" formControlName="incomeDocumentConfirmed"> Gelir belgesi kontrol edildi</label>
                  <label class="check"><input type="checkbox" formControlName="residenceDocumentConfirmed"> İkametgah kontrol edildi</label>
                  <label class="check wide"><input type="checkbox" formControlName="confirmed"> Müdür notu doğrultusunda gerekli alanlar güncellenmiş ve bilgiler kontrol edilmiştir.</label>
                </div>
                <div class="form-actions"><button type="submit">Başvuruyu Yeniden Gönder</button></div>
              </form>
            </section>
          }

          @if (revisionComparison(); as comparison) {
            <section><h2>Revizyon Karşılaştırması · Revizyon {{ comparison.revisionNumber }}</h2><div class="comparison"><div class="comparison-head"><b>Alan</b><b>Önceki Değer</b><b>Yeni Değer</b></div>@for(field of comparison.fields;track field.field){<div [class.changed]="field.changed"><b>{{field.field}}</b><span>{{field.previousValue}}</span><span>{{field.newValue}}</span></div>}</div></section>
          }

          <section><h2>Değerlendirme Geçmişi</h2><ol class="timeline">@for (history of item.history; track history.changedAtUtc) { <li><i></i><div><b>{{ statusLabel(history.newStatus) }}</b><span [title]="'İşlemi yapan: ' + history.changedBy">{{ history.changedAtUtc | date:'dd.MM.yyyy HH:mm' }} · {{ history.changedBy }}</span>@if (history.description) { <p>{{ history.description }}</p> }</div></li> }</ol></section>
        </article>
      } @else if (!error()) { <p>Başvuru yükleniyor...</p> }
    </section>

    @if (decision(); as selectedDecision) {
      <div class="overlay" (click)="decision.set(null)">
        <form class="modal decision-modal" [formGroup]="decisionForm" (ngSubmit)="saveDecision()" (click)="$event.stopPropagation()">
          <aside class="decision-menu">
            <button type="button" class="approve" [class.selected]="selectedDecision === 'Approved'" (click)="startDecision('Approved')">Onayla</button>
            <button type="button" class="reject" [class.selected]="selectedDecision === 'Rejected'" (click)="startDecision('Rejected')">Reddet</button>
            <button type="button" class="revise" [class.selected]="selectedDecision === 'Revision'" (click)="startDecision('Revision')">Revizyona Gönder</button>
          </aside>
          <div class="decision-content">
            <div class="modal-head"><h2>{{ decisionTitle(selectedDecision) }}</h2><button type="button" (click)="decision.set(null)">×</button></div>
            <p>{{ decisionDescription(selectedDecision) }}</p>
            @if (selectedDecision === 'Approved') { <label>Onaylanan Limit *<input type="number" formControlName="approvedLimit"></label> }
            @if (selectedDecision !== 'Approved') {
              <label>{{ selectedDecision === 'Rejected' ? 'Red Nedeni *' : 'Revizyon Nedeni *' }}
                <select formControlName="reasonPreset">
                  <option value="">Listeden seçin</option>
                  @for (reason of decisionReasons(selectedDecision); track reason) { <option [value]="reason">{{ reason }}</option> }
                  <option value="Other">Diğer / Kendim yazacağım</option>
                </select>
              </label>
            }
            <label>{{ selectedDecision === 'Approved' ? 'Değerlendirme Notu — isteğe bağlı' : 'Ek Açıklama' }}<textarea rows="4" formControlName="note" [placeholder]="decisionPlaceholder(selectedDecision)"></textarea></label>
            <div class="modal-actions"><button type="button" class="cancel" (click)="decision.set(null)">Vazgeç</button><button type="submit" [class]="selectedDecision.toLowerCase()">{{ decisionButton(selectedDecision) }}</button></div>
          </div>
        </form>
      </div>
    }
    @if (operationDialog(); as dialog) {
      <div class="overlay" (click)="operationDialog.set(null)"><form class="simple-modal" (ngSubmit)="saveOperation()" (click)="$event.stopPropagation()"><div class="modal-head"><h2>{{ dialog === 'cancel' ? (isManager() ? 'Başvuruyu İptal Et' : 'Başvuruyu Geri Çek') : 'Belgeyi Reddet' }}</h2><button type="button" (click)="operationDialog.set(null)">×</button></div><p>Bu işlem denetim geçmişine gerekçesiyle birlikte kaydedilir.</p><label>Gerekçe *<textarea rows="5" [formControl]="operationNote" placeholder="En az 10 karakterlik açıklama girin"></textarea></label><div class="modal-actions"><button type="button" class="cancel" (click)="operationDialog.set(null)">Vazgeç</button><button type="submit" class="rejected">Kaydet</button></div></form></div>
    }
  `,
  styles: `
    :host{display:block}.page{max-width:1240px;margin:0 auto;padding:32px;color:#20364d}.heading{display:flex;align-items:flex-end;justify-content:space-between;gap:24px;margin-bottom:22px}.heading p,.assessment-title p{margin:0 0 7px;color:#9a7418;font-size:10px;font-weight:900;letter-spacing:.18em}.heading h1{margin:0;color:#0b2a4d;font-size:34px;letter-spacing:-.04em}.heading span{display:block;margin-top:7px;color:#718093}.header-actions{display:flex;flex-wrap:wrap;justify-content:flex-end;gap:8px}.header-actions button,.header-actions a{min-height:40px;padding:0 14px;display:inline-flex;align-items:center;justify-content:center;border:1px solid #cad5df;border-radius:7px;color:#143b61;background:#fff;font:inherit;font-weight:800;text-decoration:none;cursor:pointer}.header-actions button:last-child,.header-actions a{color:#fff;border-color:#0b2a4d;background:#0b2a4d}.header-actions button:disabled{cursor:not-allowed;opacity:.55}.error,.toast{margin-bottom:16px;padding:13px 15px;border-radius:8px}.error{color:#9e3030;background:#fff0f0}.toast{display:flex;align-items:center;justify-content:space-between;color:#176044;background:#e9f7f0}.toast button{border:0;color:inherit;background:transparent;font-size:20px;cursor:pointer}
    .error{display:flex;align-items:center;justify-content:space-between}.error button{border:0;color:inherit;background:transparent;font-size:20px;cursor:pointer}
    .workflow-alert{display:grid;gap:5px;margin-bottom:18px;padding:15px 18px;border-left:4px solid #7155a4;border-radius:8px;background:#f1edfa;color:#38265c}.workflow-alert span,.workflow-alert small{color:#65587d}.assessment,.kyc{margin-bottom:18px;padding:22px;border:1px solid #d8e1e8;border-radius:11px;background:#fff}.kyc{border-left:4px solid #23845c}.kyc.blocked{border-left-color:#b44242}.kyc-grid{margin:15px 0;display:grid;grid-template-columns:repeat(2,1fr);gap:8px}.kyc-grid>div{padding:12px;display:grid;gap:4px;border-radius:7px;background:#edf8f3}.kyc-grid>div.failed{background:#fff0f0}.kyc-grid>div.review{background:#fff7df}.kyc-grid span,.kyc>small{color:#718093;font-size:11px}.assessment-title{display:flex;align-items:flex-end;justify-content:space-between;gap:15px}.assessment-title h2,.content-card h2{margin:0;color:#18344f}.assessment-title small,.assessment>small{color:#718093}.assessment-score{margin-top:16px;padding:18px 22px;display:flex;align-items:center;gap:20px;border-radius:9px;color:#fff;background:linear-gradient(100deg,#174f7b,#0b2a4d)}.assessment-score b{font-size:30px}.assessment-score span{color:#d7e4ef}.factor-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:13px 0}.factor-grid>div{padding:16px;border-radius:8px;background:#f7f9fb}.factor-grid h3{margin:0 0 9px}.factor-grid ul{margin:0;padding-left:20px}.factor-grid li{margin:6px 0;line-height:1.4}
    .content-card{padding:22px;border:1px solid #dce3e9;border-radius:11px;background:#fff}.application-strip{display:grid;grid-template-columns:repeat(3,1fr);border:1px solid #dce3e9;border-radius:8px;background:#f7f9fb;overflow:hidden}.application-strip>div{min-height:72px;padding:15px 17px;display:grid;align-content:center;gap:5px;border-right:1px solid #dce3e9}.application-strip>div:last-child{border-right:0}.application-strip span,.info-grid span{color:#7d8c9c;font-size:9px;font-weight:800;letter-spacing:.06em}.application-strip b{font-size:12px}.content-card>section{margin-top:26px}.content-card h2{margin-bottom:13px;font-size:17px}.info-grid{display:grid;grid-template-columns:repeat(3,1fr);border:1px solid #dce3e9;border-radius:8px;background:#dce3e9;gap:1px;overflow:hidden}.info-grid>div{min-height:76px;padding:15px;display:grid;align-content:center;gap:7px;background:#f8fafb}.info-grid .wide{grid-column:span 2}.info-grid b,.info-grid a{color:#20364d;font-size:12px;line-height:1.45}.info-grid a{color:#174d78;font-weight:800;text-decoration:none}.copy-button,.detail-link{padding:0;border:0;color:#174d78;background:transparent;font:inherit;font-weight:800;cursor:pointer}.copy-button{padding-left:3px}.detail-link{text-align:left}
    .process-timeline{margin:0;padding:0;display:grid;grid-template-columns:repeat(7,1fr);list-style:none}.process-timeline li{position:relative;min-width:0;display:grid;justify-items:center;text-align:center}.process-timeline li:before{content:'';position:absolute;z-index:0;top:15px;left:-50%;width:100%;height:2px;background:#dce3e9}.process-timeline li:first-child:before{display:none}.process-timeline i{position:relative;z-index:1;width:31px;height:31px;display:grid;place-items:center;border:2px solid #cfd8e0;border-radius:50%;color:#7c8b99;background:#fff;font-style:normal}.process-timeline li.completed i,.process-timeline li.completed:before{color:#fff;border-color:#23845c;background:#23845c}.process-timeline li.active i{color:#fff;border-color:#b88918;background:#b88918}.process-timeline li.rejected i{color:#fff;border-color:#b44242;background:#b44242}.process-timeline div{min-width:0;margin-top:8px;display:grid;gap:3px}.process-timeline b{font-size:11px}.process-timeline span,.process-timeline small{color:#7b8997;font-size:9px;line-height:1.35}
    .document-list{display:grid;gap:8px}.document-item{display:grid;grid-template-columns:minmax(0,1fr) auto auto auto;gap:8px;align-items:center;padding:8px;border:1px solid #dfe6ec;border-radius:7px;background:#f8fafb}.document-item>button:first-child{display:grid;grid-template-columns:150px 1fr auto;gap:10px;align-items:center;padding:6px;border:0;color:#294158;background:transparent;text-align:left;cursor:pointer}.document-item>em{padding:6px 9px;border-radius:999px;background:#fff0d4;font-size:10px;font-style:normal;font-weight:800}.document-item>em.doc-verified{color:#176044;background:#def3e8}.document-item>em.doc-rejected{color:#963737;background:#ffe2e2}.document-item>.verify,.document-item>.reject{padding:7px 9px;border:0;border-radius:6px;color:#fff;background:#23845c;font-weight:800;cursor:pointer}.document-item>.reject{background:#b44242}.document-list span,.document-list small{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.document-list small{color:#7f8c99}.document-list>p{color:#718093}.timeline{margin:0;padding:0;list-style:none}.timeline li{position:relative;padding:0 0 18px 24px}.timeline li:before{content:'';position:absolute;left:6px;top:10px;bottom:-3px;width:1px;background:#d9e1e8}.timeline li:last-child:before{display:none}.timeline i{position:absolute;left:1px;top:5px;width:11px;height:11px;border-radius:50%;background:#b88918}.timeline li div{display:grid;gap:3px}.timeline span,.timeline p{margin:0;color:#718093;font-size:11px}.quick-actions>div{display:flex;flex-wrap:wrap;gap:9px}.quick-actions a{padding:10px 13px;border:1px solid #ccd7e0;border-radius:7px;color:#174d78;text-decoration:none;font-weight:800}
    .revision-panel{padding:19px;border:1px solid #e5cc8b;border-radius:9px;background:#fffaf0}.revision-note{margin-bottom:16px;padding:12px;border-left:4px solid #b88918;background:#fff}.revision-note p{margin:5px 0 0}.form-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:12px}.form-grid label,.decision-content label{display:grid;gap:6px;font-weight:700}.form-grid .wide{grid-column:1/-1}.form-grid input,.form-grid select,.form-grid textarea,.decision-content input,.decision-content select,.decision-content textarea{width:100%;box-sizing:border-box;padding:11px;border:1px solid #cad5df;border-radius:7px;font:inherit}.form-grid .check{display:flex;align-items:flex-start}.form-grid .check input{width:auto}.form-actions{display:flex;justify-content:flex-end;margin-top:14px}.form-actions button{padding:10px 14px;border:0;border-radius:7px;color:#fff;background:#0b2a4d;font-weight:800}.comparison{border:1px solid #dce3e9;border-radius:8px;overflow:hidden}.comparison>div{display:grid;grid-template-columns:180px 1fr 1fr;gap:1px;background:#dce3e9}.comparison>div>*{padding:11px;background:#fff}.comparison-head>*{color:#718093!important;background:#f5f7f9!important;font-size:10px}.comparison .changed>*{background:#fff8e5}
    .overlay{position:fixed;z-index:200;inset:0;padding:24px;display:grid;place-items:center;background:rgba(7,25,45,.55)}.modal{width:min(780px,100%);max-height:90vh;display:grid;grid-template-columns:190px 1fr;border-radius:12px;background:#fff;box-shadow:0 28px 80px rgba(0,0,0,.28);overflow:auto}.simple-modal{width:min(520px,100%);padding:22px;border-radius:12px;background:#fff;box-shadow:0 28px 80px rgba(0,0,0,.28)}.simple-modal>p{color:#718093}.simple-modal label{display:grid;gap:7px;font-weight:800}.simple-modal textarea{padding:11px;border:1px solid #cad5df;border-radius:7px;font:inherit}.decision-menu{padding:18px;display:grid;align-content:start;gap:8px;background:#0b2a4d}.decision-menu button{padding:12px;border:1px solid rgba(255,255,255,.16);border-radius:7px;color:#dce8f2;background:transparent;text-align:left;font-weight:800;cursor:pointer}.decision-menu button.selected.approve{background:#197451}.decision-menu button.selected.reject{background:#a33f43}.decision-menu button.selected.revise{background:#9a7118}.decision-content{padding:22px}.modal-head{display:flex;align-items:center;justify-content:space-between}.modal-head h2{margin:0}.modal-head button{border:0;background:transparent;font-size:22px;cursor:pointer}.decision-content>p{color:#718093}.decision-content label{margin-top:13px}.modal-actions{margin-top:18px;display:flex;justify-content:flex-end;gap:8px}.modal-actions button{padding:10px 14px;border:0;border-radius:7px;color:#fff;font-weight:800;cursor:pointer}.modal-actions .cancel{color:#40566c;background:#e8edf2}.modal-actions .approved{background:#197451}.modal-actions .rejected{background:#a33f43}.modal-actions .revision{background:#9a7118}.danger-action{color:#fff!important;border-color:#a33f43!important;background:#a33f43!important}
    @media(max-width:900px){.heading{align-items:flex-start;flex-direction:column}.header-actions{justify-content:flex-start}.factor-grid,.info-grid{grid-template-columns:1fr 1fr}.process-timeline{grid-template-columns:1fr}.process-timeline li{grid-template-columns:32px 1fr;justify-items:start;text-align:left}.process-timeline li:before{left:15px;top:-50%;width:2px;height:100%}.process-timeline div{margin:0 0 15px 10px}.modal{grid-template-columns:1fr}.decision-menu{grid-template-columns:repeat(3,1fr)}}
    @media(max-width:650px){.page{padding:20px}.heading h1{font-size:29px}.assessment-title{align-items:flex-start;flex-direction:column}.factor-grid,.application-strip,.info-grid,.form-grid{grid-template-columns:1fr}.application-strip>div{border-right:0;border-bottom:1px solid #dce3e9}.application-strip>div:last-child{border-bottom:0}.info-grid .wide,.form-grid .wide{grid-column:auto}.document-list>button{grid-template-columns:1fr}.document-list span,.document-list small{white-space:normal}.comparison>div{grid-template-columns:1fr}.decision-menu{grid-template-columns:1fr}.overlay{padding:10px}}
  `,
})
export class ApplicationDetail implements OnInit {
  protected readonly detail = signal<CardApplicationDetail | null>(null);
  protected readonly cardTypes = signal<CardType[]>([]);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly decision = signal<Decision | null>(null);
  protected readonly preAssessment = signal<PreAssessment | null>(null);
  protected readonly revisionComparison = signal<RevisionComparison | null>(null);
  protected readonly fulfillment = signal<Fulfillment | null>(null);
  protected readonly documents = signal<ApplicationDocument[]>([]);
  protected readonly kycAssessment = signal<KycAssessment | null>(null);
  protected readonly operationDialog = signal<'cancel' | 'rejectDoc' | null>(null);
  protected readonly selectedDocument = signal<ApplicationDocument | null>(null);
  protected readonly operationNote = new FormControl('', { nonNullable: true, validators: [Validators.minLength(10)] });
  protected readonly isManager = computed(() => this.authService.currentUser()?.role === 'Manager');
  protected readonly isOfficer = computed(() => this.authService.currentUser()?.role === 'Officer');
  protected readonly decisionForm = new FormGroup({
    approvedLimit: new FormControl<number | null>(null),
    reasonPreset: new FormControl('', { nonNullable: true }),
    note: new FormControl<string | null>(null),
  });
  protected readonly revisionForm = new FormGroup({
    cardTypeId: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
    requestedLimit: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
    deliveryMethod: new FormControl<DeliveryMethod>('RegisteredAddress', { nonNullable: true }),
    deliveryAddress: new FormControl('', { nonNullable: true, validators: [Validators.minLength(10)] }),
    statementPreference: new FormControl<StatementPreference>('Email', { nonNullable: true }),
    statementDay: new FormControl(14, { nonNullable: true, validators: [Validators.pattern(/^(7|14|21|28)$/)] }),
    contactlessEnabled: new FormControl(true, { nonNullable: true }),
    internetShoppingEnabled: new FormControl(true, { nonNullable: true }),
    automaticLimitIncreaseEnabled: new FormControl(false, { nonNullable: true }),
    identityDocumentConfirmed: new FormControl(true, { nonNullable: true, validators: [Validators.requiredTrue] }),
    incomeDocumentConfirmed: new FormControl(true, { nonNullable: true, validators: [Validators.requiredTrue] }),
    residenceDocumentConfirmed: new FormControl(true, { nonNullable: true, validators: [Validators.requiredTrue] }),
    applicationNote: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
    confirmed: new FormControl(false, { nonNullable: true, validators: [Validators.requiredTrue] }),
  });
  private applicationId = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CardApplicationApiService,
    private readonly authService: AuthService,
    private readonly platformApi: PlatformApiService,
    private readonly location: Location,
  ) {}

  ngOnInit(): void {
    this.applicationId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.applicationId) { this.error.set('Geçersiz başvuru numarası.'); return; }
    this.api.getCardTypes().subscribe({ next: types => this.cardTypes.set(types) });
    this.load();
  }

  protected startDecision(decision: Decision): void {
    this.error.set('');
    this.decision.set(decision);
    this.decisionForm.setValue({
      approvedLimit: decision === 'Approved' ? this.detail()?.application.requestedLimit ?? null : null,
      reasonPreset: '',
      note: null,
    });
  }

  protected saveDecision(): void {
    const decision = this.decision();
    if (!decision) return;
    const value = this.decisionForm.getRawValue();
    const selectedReason = value.reasonPreset && value.reasonPreset !== 'Other' ? value.reasonPreset : '';
    const note = [selectedReason, value.note?.trim()].filter(Boolean).join(' — ');
    if (decision !== 'Approved' && !note) {
      this.error.set(decision === 'Rejected' ? 'Red nedeni zorunludur.' : 'Revizyon açıklaması zorunludur.');
      return;
    }
    this.api.evaluate(this.applicationId, { decision, approvedLimit: value.approvedLimit, note: note || null }).subscribe({
      next: () => { this.decision.set(null); this.load(); },
      error: response => this.error.set(response.error?.detail ?? 'Karar kaydedilemedi.'),
    });
  }

  protected resubmit(): void {
    const item = this.detail()?.application;
    if (!item || this.revisionForm.invalid) { this.revisionForm.markAllAsTouched(); this.error.set('Revizyon bilgilerini ve onay kutusunu kontrol edin.'); return; }
    const { confirmed: _confirmed, ...values } = this.revisionForm.getRawValue();
    this.api.resubmit(this.applicationId, {
      ...values,
      deliveryCity: item.deliveryCity,
      deliveryDistrict: item.deliveryDistrict,
      deliveryNeighborhood: item.deliveryNeighborhood,
      deliveryRecipientName: item.deliveryRecipientName,
      deliveryPhone: item.deliveryPhone,
      deliveryBranch: values.deliveryMethod === 'Branch' ? values.deliveryAddress : item.deliveryBranch,
      applicationNote: values.applicationNote.trim() || null,
    }).subscribe({
      next: () => this.load(),
      error: response => this.error.set(response.error?.detail ?? 'Başvuru yeniden gönderilemedi.'),
    });
  }

  protected cardRoute(id: number): string[] { return [this.isManager() ? '/manager/cards' : '/officer/cards', String(id)]; }
  protected goBack(): void { this.location.back(); }
  protected async copyApplicationNumber(value: string): Promise<void> {
    await navigator.clipboard.writeText(value);
    this.notice.set('Başvuru numarası panoya kopyalandı.');
  }
  protected showLimitExplanation(): void { this.notice.set('Azami limit; aylık gelir çarpanından diğer bankalar ve bu bankadaki aktif kart limitleri düşülerek hesaplanır. Talep bu tutarı aşabilir; onaylanan limit aşamaz.'); }
  protected showCreditScoreExplanation(score: number): void { this.notice.set(`Kredi notu simülasyonu ${score || 0}/1900. Gelir, yaş ve mevcut kart limitlerinden türetilir; gerçek KKB sorgusu değildir.`); }
  protected documentTypeLabel(value: string): string { return ({ Identity: 'Kimlik Belgesi', Income: 'Gelir Belgesi', Residence: 'İkametgah', Other: 'Diğer Belge' } as Record<string,string>)[value] ?? value; }
  protected documentVerificationLabel(value: string): string { return ({ PendingVerification: 'Yüklendi', Verified: 'Kalite Kontrolü Yapıldı', Rejected: 'Reddedildi' } as Record<string,string>)[value] ?? value; }
  protected kycStatusLabel(value: string): string { return ({ Eligible: 'İşleme Uygun', ReviewRequired: 'Manuel İnceleme Gerekli', Blocked: 'İşlem Blokeli' } as Record<string,string>)[value] ?? value; }
  protected verifyDocument(item: ApplicationDocument): void {
    this.platformApi.verifyApplicationDocument(this.applicationId, item.id, 'Verified', 'Müdür tarafından belge görüntülenerek doğrulandı.').subscribe({
      next: () => { this.notice.set('Belge doğrulandı.'); this.load(); },
      error: response => this.error.set(response.error?.detail ?? 'Belge doğrulanamadı.'),
    });
  }
  protected openDocumentRejection(item: ApplicationDocument): void {
    this.selectedDocument.set(item); this.operationNote.setValue(''); this.operationDialog.set('rejectDoc');
  }
  protected openCancellation(): void { this.operationNote.setValue(''); this.operationDialog.set('cancel'); }
  protected saveOperation(): void {
    if (this.operationNote.invalid) { this.operationNote.markAsTouched(); this.error.set('Gerekçe en az 10 karakter olmalıdır.'); return; }
    const dialog = this.operationDialog(); const note = this.operationNote.value.trim();
    const request = dialog === 'rejectDoc' && this.selectedDocument()
      ? this.platformApi.verifyApplicationDocument(this.applicationId, this.selectedDocument()!.id, 'Rejected', note)
      : this.platformApi.cancelApplication(this.applicationId, note);
    request.subscribe({
      next: () => { this.operationDialog.set(null); this.selectedDocument.set(null); this.notice.set(dialog === 'rejectDoc' ? 'Belge reddedildi ve sorumlu memura bildirildi.' : 'Başvuru işlemi kaydedildi.'); this.load(); },
      error: response => this.error.set(response.error?.detail ?? 'İşlem kaydedilemedi.'),
    });
  }
  protected downloadDocument(item: ApplicationDocument): void {
    this.api.downloadDocument(this.applicationId, item.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = window.document.createElement('a');
      anchor.href = url;
      anchor.download = item.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }
  protected statusLabel(value: string): string { return ({ Pending: 'Beklemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Revision: 'Revizyonda', Withdrawn: 'Geri Çekildi', Cancelled: 'İptal Edildi' } as Record<string, string>)[value] ?? value; }
  protected statusClass(value: string): string { return `status ${value.toLowerCase()}`; }
  protected deliveryLabel(value: string): string { return ({ RegisteredAddress: 'Kayıtlı Adrese Teslim', DifferentAddress: 'Farklı Adrese Teslim', Branch: 'Şubeden Teslim' } as Record<string, string>)[value] ?? value; }
  protected statementLabel(value: string): string { return ({ Email: 'E-posta', Paper: 'Basılı Ekstre', Mobile: 'Mobil Bildirim' } as Record<string, string>)[value] ?? value; }
  protected paymentDueLabel(statementDay: number): string {
    const dueDay = statementDay + 10;
    return dueDay <= 28 ? `ayın ${dueDay}. günü` : `takip eden ayın ${dueDay - 28}. günü`;
  }
  protected riskLabel(value: string): string { return ({ LOW: 'Düşük Risk', MEDIUM: 'Orta Risk', HIGH: 'Yüksek Risk' } as Record<string,string>)[value] ?? value; }
  protected recommendationLabel(value: string): string { return ({ APPROVAL_RECOMMENDED: 'Onay önerilir', MANUAL_REVIEW: 'Manuel inceleme', CAUTION_RECOMMENDED: 'Dikkatli inceleme' } as Record<string,string>)[value] ?? value; }
  protected downloadPdf(email: boolean): void {
    this.platformApi.applicationPdf(this.applicationId, email).subscribe({
      next: blob => {
        if (email) {
          this.notice.set('Başvuru PDF’i müşterinin kayıtlı e-posta adresine iletildi.');
          return;
        }
        const url = URL.createObjectURL(blob); const anchor = document.createElement('a');
        anchor.href = url; anchor.download = `${this.detail()?.application.applicationNumber ?? 'basvuru'}.pdf`; anchor.click(); URL.revokeObjectURL(url);
      },
      error: response => this.error.set(response.error?.detail ?? 'Başvuru PDF’i oluşturulamadı veya e-posta gönderilemedi.'),
    });
  }
  protected decisionReason(value: string | null): string {
    return value?.split(' — ')[0]?.trim() || 'Belirtilmedi';
  }
  protected decisionExplanation(value: string | null): string {
    const parts = value?.split(' — ') ?? [];
    return parts.slice(1).join(' — ').trim() || 'Ek açıklama girilmedi.';
  }
  protected processSteps(item: CardApplicationDetail): {
    key: string; label: string; state: 'completed' | 'active' | 'pending' | 'rejected';
    description: string; date: string | null;
  }[] {
    const status = item.application.status;
    const history = item.history;
    const fulfillment = this.fulfillment();
    const fulfillmentStep = (key: string) => fulfillment?.steps.find(step => step.key === key);
    const hasRevision = history.some(entry => entry.newStatus === 'Revision');
    const approvedHistory = history.find(entry => entry.newStatus === 'Approved');
    const rejectedHistory = history.find(entry => entry.newStatus === 'Rejected');
    const production = fulfillmentStep('Production');
    const printing = fulfillmentStep('Printing');
    const shipped = fulfillmentStep('Shipped');
    const delivered = fulfillmentStep('Delivered');
    return [
      { key: 'Pending', label: 'Beklemede', state: 'completed', description: 'Başvuru oluşturuldu', date: item.application.createdAtUtc },
      {
        key: 'Review', label: 'İncelemede',
        state: status === 'Pending' ? 'active' : 'completed',
        description: status === 'Pending' ? 'Müdür değerlendirmesi bekleniyor' : 'Değerlendirme tamamlandı',
        date: history[1]?.changedAtUtc ?? null,
      },
      {
        key: 'Revision', label: 'Revizyonda',
        state: status === 'Revision' ? 'active' : hasRevision ? 'completed' : 'pending',
        description: hasRevision ? 'Revizyon süreci kaydedildi' : 'Revizyon gerekmedi',
        date: history.find(entry => entry.newStatus === 'Revision')?.changedAtUtc ?? null,
      },
      {
        key: 'Decision', label: status === 'Rejected' ? 'Reddedildi' : 'Onaylandı',
        state: status === 'Rejected' ? 'rejected' : approvedHistory ? 'completed' : 'pending',
        description: status === 'Rejected' ? 'Başvuru olumsuz sonuçlandı' : approvedHistory ? 'Kart üretimi onaylandı' : 'Karar bekleniyor',
        date: rejectedHistory?.changedAtUtc ?? approvedHistory?.changedAtUtc ?? null,
      },
      {
        key: 'Printing', label: 'Kart Basıldı',
        state: printing?.status === 'Active' ? 'active' : printing?.status === 'Completed' ? 'completed' : 'pending',
        description: printing?.status === 'Active' ? 'Kart basım aşamasında' : printing?.status === 'Completed' ? 'Kart basımı tamamlandı' : 'Henüz başlamadı',
        date: printing?.timestampUtc ?? production?.timestampUtc ?? null,
      },
      {
        key: 'Shipped', label: 'Kuryede',
        state: shipped?.status === 'Active' ? 'active' : shipped?.status === 'Completed' ? 'completed' : 'pending',
        description: shipped?.status === 'Active' ? 'Kart teslimat yolunda' : shipped?.status === 'Completed' ? 'Kurye aşaması tamamlandı' : 'Henüz başlamadı',
        date: shipped?.timestampUtc ?? null,
      },
      {
        key: 'Delivered', label: 'Teslim Edildi',
        state: delivered?.status === 'Active' || delivered?.status === 'Completed' ? 'completed' : 'pending',
        description: delivered?.status === 'Active' || delivered?.status === 'Completed' ? 'Kart müşteriye teslim edildi' : 'Teslimat bekleniyor',
        date: delivered?.timestampUtc ?? null,
      },
    ];
  }
  protected decisionTitle(value: Decision): string { return value === 'Approved' ? 'Başvuruyu Onayla' : value === 'Rejected' ? 'Başvuruyu Reddet' : 'Revizyona Gönder'; }
  protected decisionDescription(value: Decision): string { return value === 'Approved' ? (this.detail()?.application.workflowStage === 'SecondManagerApproval' ? 'Bu işlem bağımsız ikinci kontroldür. Limit ilk onayla aynı olmalıdır; onaydan sonra kart kaydı oluşturulur.' : 'Yüksek tutarlı veya yüksek riskli başvurular ilk onaydan sonra bağımsız ikinci müdür onayına aktarılır.') : value === 'Rejected' ? 'Başvuru reddedildiğinde memur başvuruyu yeniden düzenleyemez.' : 'Revizyon notu memur tarafından görüntülenecek ve güncellemede kullanılacaktır.'; }
  protected decisionPlaceholder(value: Decision): string { return value === 'Approved' ? 'İsteğe bağlı değerlendirme notu girin' : value === 'Rejected' ? 'Başvurunun reddedilme nedenini açıklayın' : 'Güncellenmesi gereken bilgileri açıklayın'; }
  protected decisionButton(value: Decision): string { return value === 'Approved' ? (this.detail()?.application.workflowStage === 'SecondManagerApproval' ? 'İkinci Onayı Ver' : 'Onayı Ver') : value === 'Rejected' ? 'Başvuruyu Reddet' : 'Revizyona Gönder'; }
  protected isFirstApprover(item: CardApplicationDetail): boolean { return item.application.workflowStage === 'SecondManagerApproval' && item.application.firstApprovedByUserId === this.authService.currentUser()?.id; }
  protected decisionReasons(value: Decision): string[] {
    return value === 'Rejected'
      ? ['Kredi politikalarına uygun değil', 'Gelir yetersiz', 'Kredi notu yetersiz', 'Mevcut borçluluk yüksek', 'Belgeler doğrulanamadı']
      : ['Gelir bilgisi güncellenmeli', 'Adres bilgisi eksik', 'İletişim bilgisi doğrulanmalı', 'Talep edilen limit yeniden düzenlenmeli', 'Belge eksik'];
  }

  private load(): void {
    this.api.getDetail(this.applicationId).subscribe({
      next: data => {
        this.detail.set(data);
        this.api.getDocuments(this.applicationId).subscribe({ next: items => this.documents.set(items), error: () => this.documents.set([]) });
        this.platformApi.kycAssessment(this.applicationId).subscribe({ next: value => this.kycAssessment.set(value), error: () => this.kycAssessment.set(null) });
        this.error.set('');
        const item = data.application;
        this.revisionForm.patchValue({
          cardTypeId: item.cardTypeId,
          requestedLimit: item.requestedLimit,
          deliveryMethod: item.deliveryMethod as DeliveryMethod,
          deliveryAddress: item.deliveryBranch || item.deliveryAddress,
          statementPreference: item.statementPreference as StatementPreference,
          statementDay: item.statementDay,
          contactlessEnabled: item.contactlessEnabled,
          internetShoppingEnabled: item.internetShoppingEnabled,
          automaticLimitIncreaseEnabled: item.automaticLimitIncreaseEnabled,
          identityDocumentConfirmed: item.identityDocumentConfirmed,
          incomeDocumentConfirmed: item.incomeDocumentConfirmed,
          residenceDocumentConfirmed: item.residenceDocumentConfirmed,
          applicationNote: item.applicationNote ?? '',
          confirmed: false,
        });
        if (this.isManager()) {
          this.platformApi.preAssessment(this.applicationId).subscribe(value => this.preAssessment.set(value));
        } else {
          this.preAssessment.set(null);
        }
        this.platformApi.revisionComparison(this.applicationId).subscribe({ next: value => this.revisionComparison.set(value), error: () => this.revisionComparison.set(null) });
        this.fulfillment.set(null);
        if (data.creditCardId) {
          this.platformApi.fulfillment(data.creditCardId).subscribe({
            next: value => this.fulfillment.set(value),
            error: () => this.fulfillment.set(null),
          });
        }
      },
      error: () => this.error.set('Başvuru detayına erişilemedi.'),
    });
  }
}
