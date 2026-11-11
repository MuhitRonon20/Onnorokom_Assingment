import React, { useState } from 'react';
import type { AdmissionClass, MyHold } from '../types';
import { CheckCircle, Clock, BookOpen, AlertCircle, ArrowRight, Loader2 } from 'lucide-react';
import { api, ApiError } from '../api/client';

interface ClassListProps {
  classes: AdmissionClass[];
  loading: boolean;
  error: string | null;
  studentId: string;
  isStudentIdValid: boolean;
  myHolds: MyHold[];
  onHoldCreated: () => void;
}

export const ClassList: React.FC<ClassListProps> = ({
  classes,
  loading,
  error,
  studentId,
  isStudentIdValid,
  myHolds,
  onHoldCreated,
}) => {
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);
  const [applyError, setApplyError] = useState<{ classId: string; message: string } | null>(null);

  const handleApply = async (cls: AdmissionClass) => {
    if (!isStudentIdValid) {
      alert('Please enter a valid 17-digit Student Birth Registration Number at the top before applying.');
      return;
    }

    setApplyError(null);
    try {
      setApplyingClassId(cls.id);
      await api.createHold(cls.id, studentId);
      onHoldCreated();
    } catch (err: any) {
      if (err instanceof ApiError) {
        setApplyError({ classId: cls.id, message: err.message });
      } else {
        setApplyError({
          classId: cls.id,
          message: 'Failed to apply for seat. Please check your connection and try again.',
        });
      }
    } finally {
      setApplyingClassId(null);
    }
  };

  // Determine if this student already has an active hold or confirmed admission in this class
  const getStudentClassStatus = (classId: string) => {
    const studentHold = myHolds.find((h) => h.classId === classId);
    if (!studentHold) return null;
    return studentHold.status;
  };

  if (loading && classes.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-slate-200 p-12 text-center shadow-xs">
        <Loader2 className="w-8 h-8 animate-spin text-indigo-600 mx-auto mb-3" />
        <p className="text-sm font-semibold text-slate-700">Loading admission classes...</p>
        <p className="text-xs text-slate-400 mt-1">Fetching live seat availability from database</p>
      </div>
    );
  }

  if (error && classes.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-rose-200 p-10 text-center shadow-xs">
        <AlertCircle className="w-10 h-10 text-rose-500 mx-auto mb-3" />
        <h3 className="text-base font-bold text-slate-900">Unable to Load Classes</h3>
        <p className="text-xs text-rose-600 max-w-md mx-auto mt-1 mb-4">{error}</p>
      </div>
    );
  }

  if (classes.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-slate-200 p-12 text-center shadow-xs">
        <BookOpen className="w-10 h-10 text-slate-300 mx-auto mb-3" />
        <h3 className="text-base font-bold text-slate-800">No Admission Classes Created Yet</h3>
        <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1">
          Classes will appear here once configured by the school administrator above.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-bold text-slate-900 tracking-tight">
            Available Classes & Seat Capacities
          </h2>
          <p className="text-xs text-slate-500">
            First-come, first-served seat reservations. Live counts update automatically every few seconds.
          </p>
        </div>
        <div className="flex items-center gap-1.5 text-[11px] text-slate-500 font-medium bg-slate-100 px-2.5 py-1 rounded-full">
          <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
          Live Sync Active
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {classes.map((cls) => {
          const studentStatus = getStudentClassStatus(cls.id);
          const hasActiveApp = studentStatus === 'HELD' || studentStatus === 'CONFIRMED';
          const isFull = cls.availableSeats <= 0;
          const isApplying = applyingClassId === cls.id;
          const classError = applyError?.classId === cls.id ? applyError.message : null;

          // Seat capacity progress bar percentage
          const filledSeats = cls.heldSeats + cls.confirmedSeats;
          const percentFilled = Math.min(100, Math.round((filledSeats / cls.totalSeats) * 100));

          return (
            <div
              key={cls.id}
              className={`bg-white rounded-xl border transition-all duration-200 shadow-xs flex flex-col justify-between ${
                hasActiveApp
                  ? 'border-indigo-300 ring-1 ring-indigo-200'
                  : isFull
                  ? 'border-slate-200 opacity-80'
                  : 'border-slate-200 hover:border-slate-300 hover:shadow-md'
              }`}
            >
              <div className="p-5">
                {/* Header: Name & Badge */}
                <div className="flex items-start justify-between gap-2 mb-3">
                  <div>
                    <span className="text-[10px] font-mono font-medium text-slate-400 block">
                      ID: {cls.id}
                    </span>
                    <h3 className="text-base font-bold text-slate-900 leading-snug">
                      {cls.name}
                    </h3>
                  </div>

                  {studentStatus === 'CONFIRMED' ? (
                    <span className="shrink-0 text-[11px] font-semibold bg-emerald-100 text-emerald-800 px-2.5 py-0.5 rounded-full flex items-center gap-1">
                      <CheckCircle className="w-3 h-3" />
                      Enrolled
                    </span>
                  ) : studentStatus === 'HELD' ? (
                    <span className="shrink-0 text-[11px] font-semibold bg-amber-100 text-amber-800 px-2.5 py-0.5 rounded-full flex items-center gap-1">
                      <Clock className="w-3 h-3" />
                      Seat Held
                    </span>
                  ) : isFull ? (
                    <span className="shrink-0 text-[11px] font-semibold bg-rose-100 text-rose-800 px-2 py-0.5 rounded-full">
                      Full
                    </span>
                  ) : (
                    <span className="shrink-0 text-[11px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200 px-2 py-0.5 rounded-full">
                      {cls.availableSeats} Available
                    </span>
                  )}
                </div>

                {/* Seat Metrics Grid */}
                <div className="grid grid-cols-4 gap-1.5 py-3 border-y border-slate-100 my-3 text-center">
                  <div className="bg-slate-50 rounded-lg p-2">
                    <span className="block text-[10px] font-medium text-slate-500 uppercase">
                      Total
                    </span>
                    <span className="text-sm font-bold text-slate-800">
                      {cls.totalSeats}
                    </span>
                  </div>

                  <div className="bg-amber-50/60 rounded-lg p-2">
                    <span className="block text-[10px] font-medium text-amber-700 uppercase">
                      Held
                    </span>
                    <span className="text-sm font-bold text-amber-900">
                      {cls.heldSeats}
                    </span>
                  </div>

                  <div className="bg-emerald-50/60 rounded-lg p-2">
                    <span className="block text-[10px] font-medium text-emerald-700 uppercase">
                      Confirmed
                    </span>
                    <span className="text-sm font-bold text-emerald-900">
                      {cls.confirmedSeats}
                    </span>
                  </div>

                  <div className="bg-indigo-50/60 rounded-lg p-2">
                    <span className="block text-[10px] font-medium text-indigo-700 uppercase">
                      Available
                    </span>
                    <span className="text-sm font-bold text-indigo-950">
                      {cls.availableSeats}
                    </span>
                  </div>
                </div>

                {/* Capacity Visual Progress Bar */}
                <div className="space-y-1">
                  <div className="flex justify-between text-[11px] font-medium text-slate-500">
                    <span>Occupancy</span>
                    <span>{percentFilled}% ({filledSeats}/{cls.totalSeats})</span>
                  </div>
                  <div className="w-full bg-slate-100 rounded-full h-2 overflow-hidden flex">
                    <div
                      style={{ width: `${(cls.confirmedSeats / cls.totalSeats) * 100}%` }}
                      className="bg-emerald-500 h-full"
                      title={`Confirmed: ${cls.confirmedSeats}`}
                    />
                    <div
                      style={{ width: `${(cls.heldSeats / cls.totalSeats) * 100}%` }}
                      className="bg-amber-400 h-full"
                      title={`Held: ${cls.heldSeats}`}
                    />
                  </div>
                </div>

                {classError && (
                  <div className="mt-3 p-2 bg-rose-50 border border-rose-200 rounded-lg text-xs text-rose-700 flex items-start gap-1.5">
                    <AlertCircle className="w-3.5 h-3.5 text-rose-500 shrink-0 mt-0.5" />
                    <span>{classError}</span>
                  </div>
                )}
              </div>

              {/* Action Footer */}
              <div className="px-5 py-3.5 bg-slate-50 border-t border-slate-100 rounded-b-xl flex items-center justify-between">
                <span className="text-xs text-slate-500 font-medium">
                  {isFull
                    ? 'No seats remaining'
                    : hasActiveApp
                    ? 'Application on file'
                    : 'Instant seat hold'}
                </span>

                <button
                  type="button"
                  onClick={() => handleApply(cls)}
                  disabled={isFull || hasActiveApp || isApplying || !isStudentIdValid}
                  className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg shadow-xs flex items-center gap-1.5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  {isApplying ? (
                    <>
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      Reserving...
                    </>
                  ) : hasActiveApp ? (
                    'Applied'
                  ) : isFull ? (
                    'Class Full'
                  ) : (
                    <>
                      Apply
                      <ArrowRight className="w-3.5 h-3.5" />
                    </>
                  )}
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
