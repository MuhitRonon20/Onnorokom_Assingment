import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { validateStudentId } from '../hooks/useStudentId';
import { formatSeconds, calculateRemainingSeconds } from '../hooks/useCountdown';
import { ActiveHoldBanner } from '../components/ActiveHoldBanner';
import { ClassList } from '../components/ClassList';
import type { MyHold, AdmissionClass } from '../types';

describe('Student ID Validation', () => {
  it('should accept valid 17-digit student IDs', () => {
    const res = validateStudentId('20152691234567890');
    expect(res.isValid).toBe(true);
    expect(res.error).toBeUndefined();
  });

  it('should accept 17-digit student IDs with leading zeroes', () => {
    const res = validateStudentId('01234567890123456');
    expect(res.isValid).toBe(true);
  });

  it('should reject student IDs with non-digits', () => {
    const res = validateStudentId('2015269123456789A');
    expect(res.isValid).toBe(false);
    expect(res.error).toContain('only digits');
  });

  it('should reject student IDs with less than 17 digits', () => {
    const res = validateStudentId('1234567890123456');
    expect(res.isValid).toBe(false);
    expect(res.error).toContain('exactly 17 digits');
  });

  it('should reject student IDs with more than 17 digits', () => {
    const res = validateStudentId('123456789012345678');
    expect(res.isValid).toBe(false);
    expect(res.error).toContain('exactly 17 digits');
  });
});

describe('Countdown Utility', () => {
  it('should format seconds into MM:SS properly', () => {
    expect(formatSeconds(600)).toBe('10:00');
    expect(formatSeconds(65)).toBe('01:05');
    expect(formatSeconds(9)).toBe('00:09');
    expect(formatSeconds(0)).toBe('00:00');
    expect(formatSeconds(-5)).toBe('00:00');
  });

  it('should calculate remaining seconds from future ISO string', () => {
    const future = new Date(Date.now() + 10000).toISOString();
    const remaining = calculateRemainingSeconds(future);
    expect(remaining).toBeGreaterThan(8);
    expect(remaining).toBeLessThanOrEqual(10);
  });

  it('should return 0 for past timestamps', () => {
    const past = new Date(Date.now() - 5000).toISOString();
    expect(calculateRemainingSeconds(past)).toBe(0);
  });
});

describe('ActiveHoldBanner Component', () => {
  it('should render active hold with enabled payment and cancel buttons', () => {
    const activeHold: MyHold = {
      holdId: 'h_12345678',
      classId: 'c_abc123',
      className: 'Class 6 - Morning Shift',
      status: 'HELD',
      expiresAt: new Date(Date.now() + 300000).toISOString(),
    };

    render(
      <ActiveHoldBanner
        hold={activeHold}
        studentId="20152691234567890"
        onHoldUpdated={vi.fn()}
      />
    );

    expect(screen.getByText('Class 6 - Morning Shift')).toBeInTheDocument();
    expect(screen.getByText('ID: h_12345678')).toBeInTheDocument();
    expect(screen.getByText('Pay Admission Fee')).toBeEnabled();
    expect(screen.getByText('Cancel')).toBeEnabled();
  });

  it('should disable payment button for expired holds', () => {
    const expiredHold: MyHold = {
      holdId: 'h_expired1',
      classId: 'c_abc123',
      className: 'Class 7 - Day Shift',
      status: 'EXPIRED',
    };

    render(
      <ActiveHoldBanner
        hold={expiredHold}
        studentId="20152691234567890"
        onHoldUpdated={vi.fn()}
      />
    );

    expect(screen.getByText('Pay Admission Fee')).toBeDisabled();
    expect(screen.getByText('Cancel')).toBeDisabled();
  });
});

describe('ClassList Component', () => {
  it('should calculate and display correct available seats', () => {
    const classes: AdmissionClass[] = [
      {
        id: 'c_1',
        name: 'Class 6 - Morning Shift',
        totalSeats: 120,
        heldSeats: 15,
        confirmedSeats: 80,
        availableSeats: 25,
      },
    ];

    render(
      <ClassList
        classes={classes}
        loading={false}
        error={null}
        studentId="20152691234567890"
        isStudentIdValid={true}
        myHolds={[]}
        onHoldCreated={vi.fn()}
      />
    );

    expect(screen.getByText('Class 6 - Morning Shift')).toBeInTheDocument();
    expect(screen.getByText('25 Available')).toBeInTheDocument();
    expect(screen.getByText('Apply')).toBeEnabled();
  });

  it('should disable Apply button when class has 0 available seats', () => {
    const fullClass: AdmissionClass[] = [
      {
        id: 'c_full',
        name: 'Class 8 - Full Capacity',
        totalSeats: 100,
        heldSeats: 10,
        confirmedSeats: 90,
        availableSeats: 0,
      },
    ];

    render(
      <ClassList
        classes={fullClass}
        loading={false}
        error={null}
        studentId="20152691234567890"
        isStudentIdValid={true}
        myHolds={[]}
        onHoldCreated={vi.fn()}
      />
    );

    expect(screen.getByText('Class Full')).toBeDisabled();
  });
});

