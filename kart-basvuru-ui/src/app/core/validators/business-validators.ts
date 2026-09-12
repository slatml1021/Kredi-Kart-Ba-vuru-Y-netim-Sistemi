import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const personnelNumberPattern = /^KBP\d{6}$/;
export const personnelPasswordPattern = /^(?=.{12,64}$)(?=.*[a-zçğıöşü])(?=.*[A-ZÇĞİÖŞÜ])(?=.*\d)(?=.*[^A-Za-zÇĞİÖŞÜçğıöşü0-9]).+$/;
export const addressNamePattern = /^[\p{L}\d .'-]+$/u;
export const streetPattern = /^[\p{L}\d .,'()\/-]+$/u;
export const buildingNoPattern = /^[\p{L}\d\/-]+$/u;
export const buildingNamePattern = /^[\p{L}\d .,'()\/-]*$/u;
export const apartmentNoPattern = /^[\p{L}\d\/-]*$/u;
export const floorPattern = /^-?\d{1,3}$/;
export const postalCodePattern = /^\d{5}$/;

export function isValidTurkishIdentityNumber(value: string): boolean {
  if (!/^\d{11}$/.test(value) || value[0] === '0') return false;
  const digits = [...value].map(Number);
  const oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
  const evenSum = digits[1] + digits[3] + digits[5] + digits[7];
  const tenth = ((oddSum * 7) - evenSum) % 10;
  const eleventh = digits.slice(0, 10).reduce((sum, digit) => sum + digit, 0) % 10;
  return tenth === digits[9] && eleventh === digits[10];
}

export function turkishIdentityNumberValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '').trim();
    return !value || isValidTurkishIdentityNumber(value) ? null : { turkishIdentityNumber: true };
  };
}

export function digitsOnly(value: string, maximumLength: number): string {
  return value.replace(/\D/g, '').slice(0, maximumLength);
}
