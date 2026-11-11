import React, { useState } from 'react';
import type { MyHold, HoldStatus } from '../types';
import { FileText, CheckCircle2, Clock, Ban, Timer, CreditCard, RefreshCw, Loader2 } from 'lucide-react';
import { useCountdown } from '../hooks/useCountdown';
import { api, ApiError } from '../api/client';

interface MyApplicationsProps {
  holds: MyHold[];
  studentId: string;
  loading: boolean;
  onRefresh: () => void;
}

const HoldRowItem: React.FC<{
  hold: MyHold;
  studentId: string;
  onUpdated: () => void;
}> = ({ hold, studentId, onUpdated }) => {
  const [localStatus, setLocalStatus] = useState<HoldStatus>(hold.status);
  const [loadingAction, setLoadingAction] = useState<'confirm' | 'cancel' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  React.useEffect(() => {
    setLocalStatus(hold.status);
  }, [hold.status]);

  const { formattedTime, isExpired } = useCountdown(hold.expiresAt, () => {
    if (localStatus === 'HELD') {
      setLocalStatus('EXPIRED');
      onUpdated();
    }
  });

  const effectiveStatus: HoldStatus =
    localStatus === 'HELD' && isExpired ? 'EXPIRED' : localStatus;

  const handleConfirm = async () => {
    setActionError(null);
    try {
      setLoadingAction('confirm');
      await api.confirmHold(hold.holdId, studentId);
      setLocalStatus('CONFIRMED');
      onUpdated();
    } catch (err: any) {
      if (err instanceof ApiError) {
        setActionError(err.message);
      } else {
        setActionError('Failed to confirm hold.');
      }
    } finally {
      setLoadingAction(null);
    }
  };

  const handleCancel = async () => {
    if (!window.confirm('Are you sure you want to cancel this application?')) {
      return;
    }
    setActionError(null);
    try {
      setLoadingAction('cancel');
      await api.cancelHold(hold.holdId, studentId);
      setLocalStatus('CANCELLED');
      onUpdated();
    } catch (err: any) {
      if (err instanceof ApiError) {
        setActionError(err.message);
      } else {
        setActionError('Failed to cancel hold.');
      }
    } finally {
      setLoadingAction(null);
    }
  };

  return (
    <tr className="border-b border-slate-100 hover:bg-slate-50/70 transition-colors">
      <td className="py-3 px-4 font-mono text-xs font-semibold text-slate-700">
        {hold.holdId}
      </td>
      <td className="py-3 px-4 font-medium text-sm text-slate-900">
        {hold.className}
        <span className="block text-[10px] font-mono text-slate-400">
          ID: {hold.classId}
        </span>
      </td>
      <td className="py-3 px-4">
        <span
          className={`inline-flex items-center gap-1 text-[11px] font-bold px-2.5 py-0.5 rounded-full uppercase tracking-wider ${
            effectiveStatus === 'CONFIRMED'
              ? 'bg-emerald-100 text-emerald-800'
              : effectiveStatus === 'EXPIRED'
              ? 'bg-slate-200 text-slate-700'
              : effectiveStatus === 'CANCELLED'
              ? 'bg-rose-100 text-rose-800'
              : 'bg-amber-100 text-amber-900 animate-pulse'
          }`}
        >
          {effectiveStatus === 'CONFIRMED' && <CheckCircle2 className="w-3 h-3" />}
          {effectiveStatus === 'EXPIRED' && <Clock className="w-3 h-3" />}
          {effectiveStatus === 'CANCELLED' && <Ban className="w-3 h-3" />}
          {effectiveStatus === 'HELD' && <Timer className="w-3 h-3" />}
          {effectiveStatus}
        </span>
      </td>
      <td className="py-3 px-4 text-xs font-mono text-slate-600">
        {effectiveStatus === 'HELD' ? (
          <span className="font-bold text-amber-700 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
            {formattedTime}
          </span>
        ) : hold.expiresAt ? (
          new Date(hold.expiresAt).toLocaleTimeString()
        ) : (
          '—'
        )}
      </td>
      <td className="py-3 px-4 text-right">
        {effectiveStatus === 'HELD' ? (
          <div className="flex items-center justify-end gap-1.5">
            <button
              type="button"
              onClick={handleConfirm}
              disabled={loadingAction !== null}
              className="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-600 hover:bg-emerald-700 text-white disabled:opacity-50 flex items-center gap-1 shadow-2xs"
            >
              {loadingAction === 'confirm' ? (
                <Loader2 className="w-3 h-3 animate-spin" />
              ) : (
                <CreditCard className="w-3 h-3" />
              )}
              Pay
            </button>
            <button
              type="button"
              onClick={handleCancel}
              disabled={loadingAction !== null}
              className="px-2 py-1 text-xs font-medium rounded border border-slate-300 bg-white hover:bg-rose-50 hover:text-rose-700 hover:border-rose-300 text-slate-700 disabled:opacity-50"
            >
              {loadingAction === 'cancel' ? (
                <Loader2 className="w-3 h-3 animate-spin" />
              ) : (
                'Cancel'
              )}
            </button>
          </div>
        ) : (
          <span className="text-xs text-slate-400">—</span>
        )}
        {actionError && (
          <p className="text-[10px] text-rose-600 mt-1">{actionError}</p>
        )}
      </td>
    </tr>
  );
};

export const MyApplications: React.FC<MyApplicationsProps> = ({
  holds,
  studentId,
  loading,
  onRefresh,
}) => {
  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden mt-8">
      <div className="px-5 py-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <FileText className="w-5 h-5 text-indigo-600" />
          <div>
            <h2 className="text-sm font-bold text-slate-900 tracking-tight">
              My Applications & Seat Holds
            </h2>
            <p className="text-xs text-slate-500">
              Applications for Student ID: <span className="font-mono font-semibold text-slate-700">{studentId}</span>
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={onRefresh}
          disabled={loading}
          className="text-xs font-medium text-slate-600 hover:text-indigo-600 flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-slate-200 hover:border-slate-300 bg-white shadow-2xs transition-colors"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin text-indigo-600' : ''}`} />
          Refresh
        </button>
      </div>

      {loading && holds.length === 0 ? (
        <div className="p-8 text-center">
          <Loader2 className="w-6 h-6 animate-spin text-indigo-600 mx-auto mb-2" />
          <p className="text-xs text-slate-500">Loading student applications...</p>
        </div>
      ) : holds.length === 0 ? (
        <div className="p-8 text-center">
          <p className="text-sm font-semibold text-slate-700">No Applications on File</p>
          <p className="text-xs text-slate-400 mt-1">
            Choose an eligible admission category from the list above and click &quot;Apply&quot; to reserve a seat.
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-50/50 text-[11px] font-semibold uppercase tracking-wider text-slate-500 border-b border-slate-200">
                <th className="py-2.5 px-4">Hold ID</th>
                <th className="py-2.5 px-4">Class & Shift</th>
                <th className="py-2.5 px-4">Status</th>
                <th className="py-2.5 px-4">Expires / Time</th>
                <th className="py-2.5 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {holds.map((hold) => (
                <HoldRowItem
                  key={hold.holdId}
                  hold={hold}
                  studentId={studentId}
                  onUpdated={onRefresh}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
