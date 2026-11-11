export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface AdmissionClass {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface CreateClassPayload {
  name: string;
  totalSeats: number;
}

export interface HoldResponse {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt?: string;
}

export interface MyHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt?: string;
}

export interface ConfirmHoldResponse {
  holdId: string;
  status: HoldStatus;
}

