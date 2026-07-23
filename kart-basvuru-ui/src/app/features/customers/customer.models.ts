export interface Customer {
  id: number;
  customerNumber: string;
  nationalIdentityNumber: string;
  firstName: string;
  lastName: string;
  phoneNumber: string;
  emailAddress: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
  isActive: boolean;
}

export interface CreateCustomerRequest {
  nationalIdentityNumber: string;
  firstName: string;
  lastName: string;
  phoneNumber: string;
  emailAddress: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
}

export interface UpdateCustomerRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  emailAddress: string;
  monthlyNetIncome: number;
  otherBankTotalCardLimit: number;
}
