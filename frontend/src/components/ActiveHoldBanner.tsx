import React, { useState } from 'react';
import { Timer, CreditCard, Ban, CheckCircle2, Clock, AlertTriangle, Loader2 } from 'lucide-react';
import type { MyHold, HoldStatus } from '../types';
import { useCountdown } from '../hooks/useCountdown';
import { api, ApiError } from '../api/client';

interface ActiveHoldBannerProps {
  hold: MyHold;
  studentId: string;
  onHoldUpdated: () => void;
}

export const ActiveHoldBanner: React.FC<ActiveHoldBannerProps> = ({
  hold,
  studentId,
  onHoldUpdated,
}) => {
  const [localStatus, setLocalStatus] = useState<HoldStatus>(hold.status);
  const [actionLoading, setActionLoading] = useState<'confirm' | 'cancel' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Sync prop changes
  React.useEffect(() => {
    setLocalStatus(hold.status);
  }, [hold.status]);

  const { formattedTime, remainingSeconds, isExpired } = useCountdown(
    hold.expiresAt,
    () => {
      if (localStatus === 'HELD') {
        setLocalStatus('EXPIRED');
        onHoldUpdated();
      }
    }
  );

  const effectiveStatus: HoldStatus =
    localStatus === 'HELD' && isExpired ? 'EXPIRED' : localStatus;

  const handleConfirm = async () => {
    setActionError(null);
    setActionSuccess(null);
    try {
      setActionLoading('confirm');
      await api.confirmHold(hold.holdId, studentId);
      setLocalStatus('CONFIRMED');
      setActionSuccess('Admission fee paid successfully! Your seat is permanently confirmed.');
      onHoldUpdated();
    } catch (err: any) {
      if (err instanceof ApiError) {
        setActionError(err.message);
      } else {
        setActionError('Failed to confirm admission. Please check network connection.');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleCancel = async () => {
    if (!window.confirm('Are you sure you want to cancel this reservation? The seat will be released immediately.')) {
      return;
    }

    setActionError(null);
    setActionSuccess(null);
    try {
      setActionLoading('cancel');
      await api.cancelHold(hold.holdId, studentId);
      setLocalStatus('CANCELLED');
      setActionSuccess('Application cancelled. Your reserved seat has been released.');
      onHoldUpdated();
    } catch (err: any) {
      if (err instanceof ApiError) {
        setActionError(err.message);
      } else {
        setActionError('Failed to cancel hold. Please check network connection.');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const isHoldActive = effectiveStatus === 'HELD';
  const isUrgent = isHoldActive && remainingSeconds <= 60;

  return (
    <div
      className={`rounded-xl border p-5 mb-6 shadow-sm transition-all ${
        effectiveStatus === 'CONFIRMED'
          ? 'bg-emerald-50/60 border-emerald-300'
          : effectiveStatus === 'EXPIRED'
          ? 'bg-slate-100 border-slate-300 opacity-90'
          : effectiveStatus === 'CANCELLED'
          ? 'bg-rose-50/60 border-rose-300'
          : isUrgent
          ? 'bg-amber-50 border-amber-400 ring-2 ring-amber-300'
          : 'bg-indigo-50/70 border-indigo-200'
      }`}
    >
      <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
        {/* Left: Info */}
        <div className="space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-mono font-bold px-2.5 py-0.5 rounded-md bg-white border border-slate-300 text-slate-700 shadow-2xs">
              ID: {hold.holdId}
            </span>
            <span
              className={`text-xs font-bold px-2.5 py-0.5 rounded-full uppercase tracking-wider flex items-center gap-1 ${
                effectiveStatus === 'CONFIRMED'
                  ? 'bg-emerald-600 text-white'
                  : effectiveStatus === 'EXPIRED'
                  ? 'bg-slate-500 text-white'
                  : effectiveStatus === 'CANCELLED'
                  ? 'bg-rose-600 text-white'
                  : 'bg-amber-500 text-white'
              }`}
            >
              {effectiveStatus === 'CONFIRMED' && <CheckCircle2 className="w-3 h-3" />}
              {effectiveStatus === 'EXPIRED' && <Clock className="w-3 h-3" />}
              {effectiveStatus === 'CANCELLED' && <Ban className="w-3 h-3" />}
              {effectiveStatus === 'HELD' && <Timer className="w-3 h-3 animate-pulse" />}
              {effectiveStatus}
            </span>
            <span className="text-xs text-slate-500 font-medium">
              Category ID: {hold.classId}
            </span>
          </div>

          <h3 className="text-lg font-bold text-slate-900 pt-0.5">
            {hold.className}
          </h3>

          <p className="text-xs text-slate-600">
            {effectiveStatus === 'HELD' && (
              <>
                Your seat is held temporarily. Complete the simulated admission fee payment before time runs out.
              </>
            )}
            {effectiveStatus === 'CONFIRMED' && (
              <span className="text-emerald-800 font-medium">
                Admission confirmed! Your seat is secured for the 2026–2027 academic session.
              </span>
            )}
            {effectiveStatus === 'EXPIRED' && (
              <span className="text-slate-600">
                This hold expired without fee payment. The reserved seat was automatically returned to the class pool.
              </span>
            )}
            {effectiveStatus === 'CANCELLED' && (
              <span className="text-rose-700">
                This reservation was cancelled by the guardian.
              </span>
            )}
          </p>
        </div>

        {/* Right: Countdown & Actions */}
        <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4">
          {/* Live Countdown Display */}
          {isHoldActive && (
            <div
              className={`px-4 py-2 rounded-xl border flex items-center gap-2.5 shadow-xs ${
                isUrgent
                  ? 'bg-rose-100/90 border-rose-300 text-rose-900 animate-pulse'
                  : 'bg-white border-indigo-200 text-indigo-950'
              }`}
            >
              <Timer className={`w-5 h-5 ${isUrgent ? 'text-rose-600' : 'text-indigo-600'}`} />
              <div>
                <span className="block text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                  Remaining Time
                </span>
                <span className="text-xl font-mono font-bold tracking-wider">
                  {formattedTime}
                </span>
              </div>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex items-center gap-2 w-full sm:w-auto">
            <button
              type="button"
              onClick={handleConfirm}
              disabled={!isHoldActive || actionLoading !== null}
              className="flex-1 sm:flex-none px-4 py-2.5 text-xs font-semibold rounded-lg text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 disabled:cursor-not-allowed shadow-sm flex items-center justify-center gap-1.5 transition-colors"
            >
              {actionLoading === 'confirm' ? (
                <>
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  Processing...
                </>
              ) : (
                <>
                  <CreditCard className="w-3.5 h-3.5" />
                  Pay Admission Fee
                </>
              )}
            </button>

            <button
              type="button"
              onClick={handleCancel}
              disabled={!isHoldActive || actionLoading !== null}
              className="flex-1 sm:flex-none px-3.5 py-2.5 text-xs font-medium rounded-lg text-slate-700 bg-white border border-slate-300 hover:bg-rose-50 hover:text-rose-700 hover:border-rose-300 disabled:opacity-40 disabled:cursor-not-allowed shadow-2xs flex items-center justify-center gap-1 transition-colors"
            >
              {actionLoading === 'cancel' ? (
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
              ) : (
                <>
                  <Ban className="w-3.5 h-3.5" />
                  Cancel
                </>
              )}
            </button>
          </div>
        </div>
      </div>

      {actionError && (
        <div className="mt-3 p-2.5 bg-rose-100/80 border border-rose-300 rounded-lg flex items-center gap-2 text-xs font-medium text-rose-800">
          <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0" />
          <span>{actionError}</span>
        </div>
      )}

      {actionSuccess && (
        <div className="mt-3 p-2.5 bg-emerald-100/80 border border-emerald-300 rounded-lg flex items-center gap-2 text-xs font-medium text-emerald-800">
          <CheckCircle2 className="w-4 h-4 text-emerald-700 shrink-0" />
          <span>{actionSuccess}</span>
        </div>
      )}
    </div>
  );
};
