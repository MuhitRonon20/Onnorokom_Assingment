import type { AdmissionClass, CreateClassPayload, HoldResponse, MyHold, ConfirmHoldResponse } from '../types';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');

export class ApiError extends Error {
  status: number;
  data: any;

  constructor(status: number, message: string, data?: any) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.data = data;
  }
}

async function request<T>(
  endpoint: string,
  options: RequestInit = {},
  studentId?: string
): Promise<T> {
  const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };

  if (studentId) {
    headers['X-Student-Id'] = studentId;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (response.status === 204) {
    return {} as T;
  }

  let data: any = null;
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    data = await response.json();
  }

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    if (data?.detail) {
      message = data.detail;
    } else if (data?.title) {
      message = data.title;
    } else if (data?.message) {
      message = data.message;
    }
    throw new ApiError(response.status, message, data);
  }

  return data as T;
}

export const api = {
  // API 1: Create Class
  createClass: (payload: CreateClassPayload): Promise<AdmissionClass> =>
    request<AdmissionClass>('/classes', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  // API 2: List Classes
  getClasses: (): Promise<AdmissionClass[]> =>
    request<AdmissionClass[]>('/classes'),

  // API 3: Get Class Details
  getClass: (classId: string): Promise<AdmissionClass> =>
    request<AdmissionClass>(`/classes/${classId}`),

  // API 4: Create Hold
  createHold: (classId: string, studentId: string): Promise<HoldResponse> =>
    request<HoldResponse>(
      `/classes/${classId}/holds`,
      { method: 'POST' },
      studentId
    ),

  // API 5: Get Hold
  getHold: (holdId: string): Promise<HoldResponse> =>
    request<HoldResponse>(`/holds/${holdId}`),

  // API 6: Confirm Hold
  confirmHold: (holdId: string, studentId: string): Promise<ConfirmHoldResponse> =>
    request<ConfirmHoldResponse>(
      `/holds/${holdId}/confirm`,
      { method: 'POST' },
      studentId
    ),

  // API 7: Cancel Hold
  cancelHold: (holdId: string, studentId: string): Promise<void> =>
    request<void>(
      `/holds/${holdId}`,
      { method: 'DELETE' },
      studentId
    ),

  // API 8: My Holds
  getMyHolds: (studentId: string): Promise<MyHold[]> =>
    request<MyHold[]>('/me/holds', { method: 'GET' }, studentId),
};
