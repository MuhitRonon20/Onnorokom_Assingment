import React, { useState } from 'react';
import { PlusCircle, Loader2, CheckCircle2, AlertTriangle, Shield } from 'lucide-react';
import { api, ApiError } from '../api/client';

interface AdminCreateClassProps {
  onClassCreated: () => void;
}

export const AdminCreateClass: React.FC<AdminCreateClassProps> = ({ onClassCreated }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState<number | ''>(100);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    const trimmedName = name.trim();
    if (!trimmedName) {
      setError('Please provide a valid class and shift name.');
      return;
    }

    const seatsNum = Number(totalSeats);
    if (!seatsNum || seatsNum <= 0 || !Number.isInteger(seatsNum)) {
      setError('Seat capacity must be a positive whole integer greater than zero.');
      return;
    }

    try {
      setLoading(true);
      const created = await api.createClass({
        name: trimmedName,
        totalSeats: seatsNum,
      });

      setSuccess(`Class "${created.name}" created successfully with ${created.totalSeats} seats (ID: ${created.id}).`);
      setName('');
      setTotalSeats(100);
      onClassCreated();
      setTimeout(() => setSuccess(null), 6000);
    } catch (err: any) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to create class. Please check network connection.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden mb-6">
      <div
        className="px-5 py-3.5 bg-slate-50 border-b border-slate-200 flex items-center justify-between cursor-pointer hover:bg-slate-100/70 transition-colors"
        onClick={() => setIsOpen(!isOpen)}
      >
        <div className="flex items-center gap-2">
          <Shield className="w-4 h-4 text-indigo-600" />
          <h2 className="text-sm font-semibold text-slate-800">
            Administrator — Class & Shift Management
          </h2>
          <span className="text-[11px] bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded-full font-medium">
            Admin Action
          </span>
        </div>
        <button
          type="button"
          className="text-xs font-medium text-indigo-600 hover:text-indigo-800 flex items-center gap-1"
        >
          {isOpen ? 'Close Form' : '+ Add New Admission Category'}
        </button>
      </div>

      {isOpen && (
        <form onSubmit={handleSubmit} className="p-5">
          {error && (
            <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-lg flex items-start gap-2.5 text-rose-800 text-sm">
              <AlertTriangle className="w-5 h-5 text-rose-500 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Creation Failed</p>
                <p className="text-xs text-rose-700 mt-0.5">{error}</p>
              </div>
            </div>
          )}

          {success && (
            <div className="mb-4 p-3 bg-emerald-50 border border-emerald-200 rounded-lg flex items-start gap-2.5 text-emerald-800 text-sm">
              <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Success</p>
                <p className="text-xs text-emerald-700 mt-0.5">{success}</p>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-12 gap-4">
            <div className="md:col-span-8">
              <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                Class & Shift Name <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Class 6 - Morning Shift"
                className="w-full px-3.5 py-2 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
                disabled={loading}
              />
              <p className="text-[11px] text-slate-400 mt-1">
                Must be unique across all classes and shifts.
              </p>
            </div>

            <div className="md:col-span-4">
              <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                Total Seat Capacity <span className="text-rose-500">*</span>
              </label>
              <input
                type="number"
                min={1}
                max={10000}
                value={totalSeats}
                onChange={(e) => setTotalSeats(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
                className="w-full px-3.5 py-2 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
                disabled={loading}
              />
              <p className="text-[11px] text-slate-400 mt-1">
                Initial capacity of seats to open.
              </p>
            </div>
          </div>

          <div className="mt-4 flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsOpen(false)}
              className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800 rounded-lg hover:bg-slate-100 transition-colors"
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg shadow-sm flex items-center gap-2 disabled:opacity-50 transition-colors"
            >
              {loading ? (
                <>
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  Creating Class...
                </>
              ) : (
                <>
                  <PlusCircle className="w-3.5 h-3.5" />
                  Create Admission Category
                </>
              )}
            </button>
          </div>
        </form>
      )}
    </div>
  );
};

