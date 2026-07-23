export interface CardType {
  id: number;
  name: string;
  minimumLimit: number | null;
  maximumLimit: number | null;
}

export interface CreateCardApplicationRequest {
  customerId: number;
  cardTypeId: number;
  requestedLimit: number;
}

export interface EvaluateCardApplicationRequest {
  decision: 'Approved' | 'Rejected' | 'Revision';
  approvedLimit: number | null;
  note: string | null;
}

export interface ResubmitCardApplicationRequest {
  cardTypeId: number;
  requestedLimit: number;
}

export interface CardApplication {
  id: number;
  applicationNumber: string;
  customerId: number;
  customerNumber: string;
  customerFullName: string;
  cardTypeId: number;
  cardTypeName: string;
  requestedLimit: number;
  availableLimit: number;
  status: string;
  createdAtUtc: string;
}

export interface ApplicationHistoryItem { previousStatus: string | null; newStatus: string; description: string | null; changedBy: string; changedAtUtc: string; }
export interface CardApplicationDetail { application: CardApplication; approvedLimit: number | null; evaluationNote: string | null; creditCardId: number | null; history: ApplicationHistoryItem[]; }
