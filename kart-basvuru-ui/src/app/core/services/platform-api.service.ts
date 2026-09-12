import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';

export interface PreAssessment {
  score: number; riskLevel: string; recommendation: string;
  positiveFactors: string[]; riskFactors: string[]; disclaimer: string;
}
export interface DuplicateApplication {
  hasDuplicate: boolean; message: string; applicationId: number | null;
  applicationNumber: string | null; status: string | null; applicationDate: string | null;
}
export interface Fulfillment {
  creditCardId: number; status: string; estimatedPrintAtUtc: string; estimatedDeliveryAtUtc: string;
  trackingNumber: string | null;
  steps: { key: string; label: string; status: string; timestampUtc: string | null }[];
}
export interface SlaDashboard {
  averageEvaluationMinutes: number; overdueToday: number; longestWaitingHours: number;
  revisionReturnRate: number;
  longestWaiting: { applicationId: number; applicationNumber: string; customer: string; status: string; waitingHours: number; slaStatus: string }[];
}
export interface AppNotification {
  id: number; type: string; title: string; message: string; link: string | null; isRead: boolean; createdAtUtc: string;
}
export interface ChatUser { id: number; fullName: string; role: string; }
export interface ChatMessage {
  id: number; senderUserId: number; senderName: string; recipientUserId: number; recipientName: string;
  message: string; applicationId: number | null; isRead: boolean; createdAtUtc: string;
}
export interface SupplementaryApplication {
  id: number; applicationNumber: string; primaryCreditCardId: number; primaryCustomerId: number; primaryCustomer: string;
  holderCustomerId: number; holderCustomer: string; relationship: string; requestedLimit: number;
  deliveryMethod: string; deliveryAddress: string; status: string; createdAtUtc: string;
  evaluationNote: string | null; evaluatedAtUtc: string | null;
  maskedCardNumber: string | null; issuedAtUtc: string | null;
  cardStatus: string; fulfillmentStatus: string | null;
  estimatedPrintAtUtc: string | null; estimatedDeliveryAtUtc: string | null;
  deliveredAtUtc: string | null; trackingNumber: string | null;
}
export interface SupplementaryCardContext {
  primaryCreditCardId: number; maskedCardNumber: string; primaryCardStatus: string; primaryCardLimit: number;
  usedSupplementaryLimit: number; reservedSupplementaryLimit: number; availableSupplementaryLimit: number;
  primaryCustomerId: number; primaryCustomerNumber: string; primaryCustomerFullName: string;
  primaryCustomerNationalIdentityNumber: string; primaryCustomerIsActive: boolean;
  primaryCustomerBirthDate: string | null; registeredDeliveryAddress: string;
}
export interface SimulationResult {
  canCreateApplication: boolean; missingFields: string[];
  recommendedCard: string; suggestedMinimumLimit: number; suggestedMaximumLimit: number;
  preAssessment: PreAssessment; riskReasons: string[]; systemRecommendations: string[];
  disclaimer: string;
}
export interface Consent {
  id: number; consentType: string; textVersion: string; isGranted: boolean; channel: string;
  capturedBy: string; capturedAtUtc: string; withdrawnAtUtc: string | null;
}
export interface RevisionComparison {
  revisionNumber: number;
  fields: { field: string; previousValue: string; newValue: string; changed: boolean }[];
}
export interface UserProfile {
  id: number; fullName: string; registrationNumber: string; role: string; corporateEmail: string;
  phoneNumber: string; title: string; department: string; branch: string; isActive: boolean;
  registeredAtUtc: string; lastSuccessfulLoginUtc: string | null; lastFailedLoginUtc: string | null;
  passwordChangedAtUtc: string | null; twoFactorEnabled: boolean; isLocked: boolean;
  loginHistory: { dateUtc: string; isSuccessful: boolean; device: string; ipAddress: string | null }[];
  recentActivities: { dateUtc: string; description: string }[];
  grantedPermissions: string[]; deniedPermissions: string[];
  activeOfficerCount: number | null; applicationsToday: number | null; revisionWaiting: number | null; overdueApplications: number | null;
  notifyApplicationEvents: boolean; notifySlaWarnings: boolean; notifySecurityEvents: boolean;
}
export interface WorkflowOfficer {
  userId: number; fullName: string; registrationNumber: string; branch: string;
  openWorkCount: number; revisionCount: number; overdueCount: number;
}
export interface WorkflowApplication {
  id: number; applicationNumber: string; customerId: number; customer: string;
  cardType: string; requestedLimit: number; status: string; riskLevel: string; score: number;
  assignedOfficerUserId: number | null; assignedOfficer: string | null; assignedAtUtc: string | null;
  requiresSecondApproval: boolean; workflowStage: string; priority: string; waitingHours: number;
  slaStatus: string; createdAtUtc: string; firstApprovedByUserId: number | null;
  firstApprover: string | null; firstApprovedAtUtc: string | null; evaluatedAtUtc: string | null;
}
export interface WorkflowOverview {
  officers: WorkflowOfficer[]; applications: WorkflowApplication[]; completedApplications: WorkflowApplication[];
  unassignedCount: number; secondApprovalCount: number; overdueCount: number;
}
export interface KycAssessment {
  overallStatus: string; checkedAtUtc: string; disclaimer: string;
  checks: { key: string; label: string; status: string; detail: string; blocking: boolean }[];
}
export interface DecisionQuality {
  periodDays: number; totalDecisions: number; approvalRate: number; rejectionRate: number;
  revisionRate: number; averageEvaluationMinutes: number; dualApprovalCount: number;
  overdueOpenCount: number; reassignmentCount: number;
  managers: { userId: number; manager: string; total: number; approved: number; rejected: number;
    revision: number; approvalRate: number; averageMinutes: number }[];
}
export interface AuditItem {
  id: number; action: string; entityName: string; entityId: string | null; detail: string | null;
  user: string | null; ipAddress: string | null; createdAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class PlatformApiService {
  constructor(private readonly http: HttpClient) {}

  preAssessment(id: number) { return this.http.get<PreAssessment>(`/api/card-applications/${id}/pre-assessment`); }
  duplicateCheck(customerId: number, cardTypeId: number) {
    return this.http.get<DuplicateApplication>('/api/card-applications/duplicate-check', {
      params: new HttpParams().set('customerId', customerId).set('cardTypeId', cardTypeId),
    });
  }
  revisionComparison(id: number) { return this.http.get<RevisionComparison>(`/api/card-applications/${id}/revision-comparison`); }
  fulfillment(cardId: number) { return this.http.get<Fulfillment>(`/api/cards/${cardId}/fulfillment`); }
  sla() { return this.http.get<SlaDashboard>('/api/platform/sla'); }
  notifications() { return this.http.get<AppNotification[]>('/api/platform/notifications'); }
  markNotificationRead(id: number) { return this.http.post<void>(`/api/platform/notifications/${id}/read`, {}); }
  markAllNotificationsRead() { return this.http.post<void>('/api/platform/notifications/read-all', {}); }
  chatUsers() { return this.http.get<ChatUser[]>('/api/platform/chat/users'); }
  conversation(userId: number) { return this.http.get<ChatMessage[]>(`/api/platform/chat/${userId}`); }
  sendMessage(recipientUserId: number, message: string, applicationId: number | null = null) {
    return this.http.post<ChatMessage>('/api/platform/chat', { recipientUserId, message, applicationId });
  }
  sendBulkMessage(recipientUserIds: number[], ccRecipientUserIds: number[], message: string, applicationId: number | null = null) {
    return this.http.post<ChatMessage[]>('/api/platform/chat/bulk', { recipientUserIds, ccRecipientUserIds, message, applicationId });
  }
  reportMessage(messageId: number, reason: 'Spam' | 'Uygunsuz İçerik' | 'Şüpheli Bağlantı' | 'Diğer', note: string | null = null) {
    return this.http.post<void>(`/api/platform/chat/${messageId}/report`, { reason, note });
  }
  supplementary() { return this.http.get<SupplementaryApplication[]>('/api/supplementary-card-applications'); }
  supplementaryById(id: number) { return this.http.get<SupplementaryApplication>(`/api/supplementary-card-applications/${id}`); }
  supplementaryCardContext(primaryCardId: number) {
    return this.http.get<SupplementaryCardContext>(
      `/api/supplementary-card-applications/card-context/${primaryCardId}`,
    );
  }
  createSupplementary(request: {
    primaryCreditCardId: number; supplementaryHolderCustomerId: number;
    relationship: string; requestedLimit: number; deliveryMethod: string; deliveryAddress: string | null;
  }) {
    return this.http.post<SupplementaryApplication>('/api/supplementary-card-applications', request);
  }
  evaluateSupplementary(id: number, decision: 'Approved' | 'Rejected', note: string | null) {
    return this.http.post<SupplementaryApplication>(`/api/supplementary-card-applications/${id}/evaluation`, { decision, note });
  }
  profile() { return this.http.get<UserProfile>('/api/platform/profile'); }
  updateProfile(request: {
    phoneNumber: string; profilePhotoUrl: string | null; notifyApplicationEvents: boolean;
    notifySlaWarnings: boolean; notifySecurityEvents: boolean;
  }) { return this.http.put<UserProfile>('/api/platform/profile', request); }
  customerConsents(customerId: number) { return this.http.get<Consent[]>(`/api/platform/customers/${customerId}/consents`); }
  setConsent(customerId: number, consentType: string, isGranted: boolean) {
    return this.http.post<Consent>(`/api/platform/customers/${customerId}/consents`, {
      consentType, textVersion: 'v2.1', isGranted, channel: 'Şube',
    });
  }
  kkb(customerId: number) { return this.http.get<unknown>(`/api/platform/customers/${customerId}/kkb-analysis`); }
  simulate(request: { customerId: number; cardTypeId: number; requestedLimit: number; deliveryMethod: string }) {
    return this.http.post<SimulationResult>('/api/platform/simulation', request);
  }
  applicationPdf(id: number, email = false) {
    return this.http.get(`/api/card-applications/${id}/pdf`, { params: { email }, responseType: 'blob' });
  }
  cardPdf(id: number, email = false) {
    return this.http.get(`/api/cards/${id}/pdf`, { params: { email }, responseType: 'blob' });
  }
  myWork() { return this.http.get<WorkflowApplication[]>('/api/application-workflow/my-work'); }
  claimApplication(applicationId: number) {
    return this.http.post<WorkflowApplication>(`/api/application-workflow/${applicationId}/claim`, {});
  }
  workflowOverview() { return this.http.get<WorkflowOverview>('/api/application-workflow/overview'); }
  assignApplication(applicationId: number, officerUserId: number, reason: string | null = null) {
    return this.http.post<WorkflowApplication>(`/api/application-workflow/${applicationId}/assign`, { officerUserId, reason });
  }
  autoAssignApplication(applicationId: number) {
    return this.http.post<WorkflowApplication>(`/api/application-workflow/${applicationId}/auto-assign`, {});
  }
  kycAssessment(applicationId: number) {
    return this.http.get<KycAssessment>(`/api/application-operations/${applicationId}/kyc`);
  }
  verifyApplicationDocument(applicationId: number, documentId: number,
    status: 'Verified' | 'Rejected', note: string | null, expiresAtUtc: string | null = null) {
    return this.http.post<void>(`/api/application-operations/${applicationId}/documents/${documentId}/verification`,
      { status, note, expiresAtUtc });
  }
  cancelApplication(applicationId: number, reason: string) {
    return this.http.post<void>(`/api/application-operations/${applicationId}/cancel`, { reason });
  }
  decisionQuality(days = 30) {
    return this.http.get<DecisionQuality>('/api/application-operations/quality', { params: { days } });
  }
  audit(search = '', action = '') {
    let params = new HttpParams().set('limit', 200);
    if (search) params = params.set('search', search);
    if (action) params = params.set('action', action);
    return this.http.get<AuditItem[]>('/api/application-operations/audit', { params });
  }
}
