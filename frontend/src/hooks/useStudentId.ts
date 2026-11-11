import { useState, useEffect } from 'react';

const STORAGE_KEY = 'school_admission_student_id';
const DEFAULT_STUDENT_ID = '20152691234567890';

export function validateStudentId(id: string): { isValid: boolean; error?: string } {
  const trimmed = id.trim();
  if (!trimmed) {
    return { isValid: false, error: 'Student ID is required.' };
  }
  if (!/^\d+$/.test(trimmed)) {
    return { isValid: false, error: 'Student ID must contain only digits.' };
  }
  if (trimmed.length !== 17) {
    return {
      isValid: false,
      error: `Student ID must be exactly 17 digits (currently ${trimmed.length}).`,
    };
  }
  return { isValid: true };
}

export function useStudentId() {
  const [studentId, setStudentIdState] = useState<string>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return saved ? saved : DEFAULT_STUDENT_ID;
    } catch {
      return DEFAULT_STUDENT_ID;
    }
  });

  const { isValid, error: validationError } = validateStudentId(studentId);

  const setStudentId = (newId: string) => {
    setStudentIdState(newId);
    try {
      localStorage.setItem(STORAGE_KEY, newId);
    } catch (e) {
      console.error('Failed to save student ID to localStorage', e);
    }
  };

  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (!saved) {
        localStorage.setItem(STORAGE_KEY, studentId);
      }
    } catch (e) {
      console.error('Failed to access localStorage', e);
    }
  }, [studentId]);

  return {
    studentId,
    setStudentId,
    isValid,
    validationError,
  };
}

