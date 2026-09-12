export interface Customer {
  id: number;
  customerNumber: string;
  nationalIdentityNumber: string;
  firstName: string;
  lastName: string;
  phoneCountryCode: string;
  phoneNumber: string;
  isPhoneVerified: boolean;
  emailAddress: string;
  isEmailVerified: boolean;
  birthDate: string | null;
  gender: string;
  educationLevel: string;
  occupation: string;
  employmentStatus: string;
  city: string;
  district: string;
  neighborhood: string;
  address: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
  creditScore: number;
  isActive: boolean;
  isProfileComplete: boolean;
  missingProfileFields: string[];
  ownBankTotalCardLimit: number;
  availableCardLimit: number;
  otherBankCards: OtherBankCard[];
}

export interface OtherBankCard {
  id: number;
  bankName: string;
  maskedCardNumber: string | null;
  cardLimit: number;
  isActive: boolean;
}

export interface OtherBankCardRequest {
  bankName: string;
  maskedCardNumber: string | null;
  cardLimit: number;
}

export interface ExternalRiskProfile {
  otherBankTotalCardLimit: number;
  cards: Array<{
    bankName: string;
    cardLastFourDigits: string;
    cardLimit: number;
  }>;
}

export interface CreateCustomerRequest {
  nationalIdentityNumber: string;
  firstName: string;
  lastName: string;
  phoneCountryCode: string;
  phoneNumber: string;
  emailAddress: string;
  birthDate: string | null;
  gender: string;
  educationLevel: string;
  occupation: string;
  employmentStatus: string;
  city: string;
  district: string;
  neighborhood: string;
  address: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
  otherBankCards: OtherBankCardRequest[];
  kvkkConsentGranted: boolean;
  smsConsentGranted: boolean;
  emailConsentGranted: boolean;
  street: string;
  avenue: string | null;
  buildingNo: string;
  apartmentNo: string | null;
  floor: string | null;
  postalCode: string;
}

export interface UpdateCustomerRequest {
  firstName: string;
  lastName: string;
  phoneCountryCode: string;
  phoneNumber: string;
  emailAddress: string;
  birthDate: string | null;
  gender: string;
  educationLevel: string;
  occupation: string;
  employmentStatus: string;
  city: string;
  district: string;
  neighborhood: string;
  address: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
  otherBankCards: OtherBankCardRequest[];
}

export interface ContactVerification {
  channel: 'Phone' | 'Email';
  maskedDestination: string;
  expiresAtUtc: string;
  demoCode: string;
}

export interface CustomerAddress {
  id: number;
  customerId: number;
  name: string;
  city: string;
  district: string;
  neighborhood: string;
  street: string;
  avenue: string | null;
  buildingNo: string;
  apartmentNo: string | null;
  floor: string | null;
  postalCode: string;
  fullAddress: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface CustomerAddressRequest {
  name: string;
  city: string;
  district: string;
  neighborhood: string;
  street: string;
  avenue: string | null;
  buildingNo: string;
  apartmentNo: string | null;
  floor: string | null;
  postalCode: string;
  isDefault: boolean;
}

export interface CustomerCardSummary {
  id: number;
  maskedCardNumber: string;
  cardTypeName: string;
  cardLimit: number;
  status: string;
}

export interface CustomerApplicationSummary {
  id: number;
  applicationNumber: string;
  cardTypeName: string;
  requestedLimit: number;
  status: string;
  createdAtUtc: string;
}

export interface CustomerDetailData {
  customer: Customer;
  cards: CustomerCardSummary[];
  applications: CustomerApplicationSummary[];
}
