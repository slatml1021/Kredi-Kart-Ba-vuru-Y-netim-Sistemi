export type UserRole = 'Officer' | 'Manager';

export interface CurrentUser {
  id: number;
  registrationNumber: string;
  fullName: string;
  role: UserRole;
}

export interface LoginRequest {
  registrationNumber: string;
  password: string;
}

interface AuthenticatedUserResponse {
  id: number;
  registrationNumber: string;
  fullName: string;
  roles: UserRole[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUserResponse;
}
