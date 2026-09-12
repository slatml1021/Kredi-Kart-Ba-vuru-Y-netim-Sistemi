export interface CardType {
  id: number;
  name: string;
  productName: string;
  network: 'Visa' | 'Mastercard' | 'TROY';
  bin: string;
  minimumLimit: number | null;
  maximumLimit: number | null;
  productDescription: string;
  annualFee: string;
  benefits: string[];
}

export interface CreateCardApplicationRequest {
  customerId: number;
  cardTypeId: number;
  requestedLimit: number;
  deliveryAddress: string;
  deliveryMethod: 'RegisteredAddress' | 'DifferentAddress' | 'Branch';
  deliveryCity: string | null;
  deliveryDistrict: string | null;
  deliveryNeighborhood: string | null;
  deliveryRecipientName: string | null;
  deliveryPhone: string | null;
  deliveryBranch: string | null;
  statementPreference: 'Email' | 'Paper' | 'Mobile';
  contactlessEnabled: boolean;
  internetShoppingEnabled: boolean;
  applicationNote: string | null;
  statementDay: number;
  automaticLimitIncreaseEnabled: boolean;
  duplicateWarningAcknowledged: boolean;
  identityDocumentConfirmed: boolean;
  incomeDocumentConfirmed: boolean;
  residenceDocumentConfirmed: boolean;
}

export interface EvaluateCardApplicationRequest {
  decision: 'Approved' | 'Rejected' | 'Revision';
  approvedLimit: number | null;
  note: string | null;
}

export interface ResubmitCardApplicationRequest {
  cardTypeId: number;
  requestedLimit: number;
  deliveryAddress: string;
  deliveryMethod: 'RegisteredAddress' | 'DifferentAddress' | 'Branch';
  deliveryCity: string | null;
  deliveryDistrict: string | null;
  deliveryNeighborhood: string | null;
  deliveryRecipientName: string | null;
  deliveryPhone: string | null;
  deliveryBranch: string | null;
  statementPreference: 'Email' | 'Paper' | 'Mobile';
  contactlessEnabled: boolean;
  internetShoppingEnabled: boolean;
  applicationNote: string | null;
  statementDay: number;
  automaticLimitIncreaseEnabled: boolean;
  identityDocumentConfirmed: boolean;
  incomeDocumentConfirmed: boolean;
  residenceDocumentConfirmed: boolean;
}

export interface CardApplication {
  id: number;
  applicationNumber: string;
  customerId: number;
  customerNumber: string;
  customerFullName: string;
  customerNationalIdentityNumber: string;
  customerPhoneCountryCode: string;
  customerPhoneNumber: string;
  customerEmailAddress: string;
  customerMonthlyNetIncome: number;
  customerOtherBankTotalCardLimit: number;
  customerCreditScore: number;
  cardTypeId: number;
  cardTypeName: string;
  requestedLimit: number;
  availableLimit: number;
  deliveryMethod: string;
  deliveryAddress: string;
  deliveryCity: string | null;
  deliveryDistrict: string | null;
  deliveryNeighborhood: string | null;
  deliveryRecipientName: string | null;
  deliveryPhone: string | null;
  deliveryBranch: string | null;
  statementPreference: string;
  statementDay: number;
  contactlessEnabled: boolean;
  internetShoppingEnabled: boolean;
  automaticLimitIncreaseEnabled: boolean;
  identityDocumentConfirmed: boolean;
  incomeDocumentConfirmed: boolean;
  residenceDocumentConfirmed: boolean;
  preAssessmentScore: number;
  preAssessmentRiskLevel: string;
  preAssessmentRecommendation: string;
  applicationNote: string | null;
  status: string;
  createdAtUtc: string;
  isEvaluationBlocked: boolean;
  assignedOfficerUserId: number | null;
  assignedOfficerName: string | null;
  assignedAtUtc: string | null;
  requiresSecondApproval: boolean;
  workflowStage: string;
  firstApprovedByUserId: number | null;
  firstApproverName: string | null;
  firstApprovedAtUtc: string | null;
  approvalPolicy: string;
  autoAssignmentCompleted: boolean;
  lastAssignmentReason: string | null;
  escalationLevel: number;
  cancellationReason: string | null;
  cancelledAtUtc: string | null;
  cancelledByName: string | null;
}

export interface ApplicationHistoryItem { previousStatus: string | null; newStatus: string; description: string | null; changedBy: string; changedAtUtc: string; }
export interface ApplicationDocument {
  id: number; applicationId: number; documentType: string; fileName: string; sha256: string; uploadedAtUtc: string;
  verificationStatus: string; verifiedBy: string | null; verifiedAtUtc: string | null;
  verificationNote: string | null; expiresAtUtc: string | null;
}
export interface CardApplicationDetail { application: CardApplication; approvedLimit: number | null; evaluationNote: string | null; creditCardId: number | null; history: ApplicationHistoryItem[]; }
