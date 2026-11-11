import React from 'react';
import { School, UserCheck, AlertCircle, RefreshCw } from 'lucide-react';

interface HeaderProps {
  studentId: string;
  onStudentIdChange: (id: string) => void;
  isValid: boolean;
  validationError?: string;
  onRefreshAll: () => void;
  isRefreshing: boolean;
}

const SAMPLE_STUDENTS = [
  { label: 'Student A', id: '20152691234567890' },
  { label: 'Student B', id: '20152691234567891' },
  { label: 'Student C', id: '20152691234567892' },
];

export const Header: React.FC<HeaderProps> = ({
  studentId,
  onStudentIdChange,
  isValid,
  validationError,
  onRefreshAll,
  isRefreshing,
}) => {
  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-30 shadow-sm">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          {/* Logo & School Branding */}
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 bg-indigo-600 rounded-xl flex items-center justify-center text-white shadow-md shadow-indigo-100">
              <School className="w-6 h-6" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900 tracking-tight">
                Saint Jude International School
              </h1>
              <p className="text-xs font-medium text-slate-500">
                Online Admission & Seat Reservation Portal • Academic Year 2026–2027
              </p>
            </div>
          </div>

          {/* Student Identity Controls */}
          <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
            <div className="w-full sm:w-auto">
              <label
                htmlFor="student-id-input"
                className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1"
              >
                Student Birth Registration No. (17 Digits)
              </label>
              <div className="relative">
                <input
                  id="student-id-input"
                  type="text"
                  maxLength={17}
                  value={studentId}
                  onChange={(e) => onStudentIdChange(e.target.value.replace(/\D/g, ''))}
                  placeholder="e.g. 20152691234567890"
                  className={`w-full sm:w-64 px-3 py-2 text-sm font-mono tracking-wider rounded-lg border focus:outline-none focus:ring-2 transition-all ${
                    isValid
                      ? 'border-emerald-300 bg-emerald-50/30 text-slate-900 focus:ring-emerald-500 focus:border-emerald-500'
                      : 'border-amber-300 bg-amber-50/30 text-amber-900 focus:ring-amber-500 focus:border-amber-500'
                  }`}
                />
                <div className="absolute right-2.5 top-2.5 pointer-events-none">
                  {isValid ? (
                    <UserCheck className="w-4 h-4 text-emerald-600" />
                  ) : (
                    <AlertCircle className="w-4 h-4 text-amber-500" />
                  )}
                </div>
              </div>
              {validationError && (
                <p className="text-[11px] font-medium text-amber-600 mt-1">
                  {validationError}
                </p>
              )}
            </div>

            {/* Quick Switch Preset Buttons */}
            <div className="flex items-center gap-1.5 self-end sm:self-center mt-4 sm:mt-5">
              <span className="text-xs text-slate-400 font-medium mr-1 hidden lg:inline">
                Quick:
              </span>
              {SAMPLE_STUDENTS.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => onStudentIdChange(s.id)}
                  className={`px-2 py-1 text-xs font-medium rounded-md border transition-colors ${
                    studentId === s.id
                      ? 'bg-indigo-50 border-indigo-300 text-indigo-700 font-semibold'
                      : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {s.label}
                </button>
              ))}

              <button
                type="button"
                onClick={onRefreshAll}
                disabled={isRefreshing}
                title="Refresh All Data"
                className="p-1.5 ml-1 text-slate-500 hover:text-indigo-600 rounded-md hover:bg-slate-100 transition-colors disabled:opacity-50"
              >
                <RefreshCw className={`w-4 h-4 ${isRefreshing ? 'animate-spin text-indigo-600' : ''}`} />
              </button>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};

