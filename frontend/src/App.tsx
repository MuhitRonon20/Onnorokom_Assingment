import { useState, useEffect, useCallback, useMemo } from 'react';
import { Header } from './components/Header';
import { AdminCreateClass } from './components/AdminCreateClass';
import { ActiveHoldBanner } from './components/ActiveHoldBanner';
import { ClassList } from './components/ClassList';
import { MyApplications } from './components/MyApplications';
import { useStudentId } from './hooks/useStudentId';
import { api } from './api/client';
import type { AdmissionClass, MyHold } from './types';
import { AlertTriangle } from 'lucide-react';

export function App() {
  const { studentId, setStudentId, isValid: isStudentIdValid, validationError } = useStudentId();

  const [classes, setClasses] = useState<AdmissionClass[]>([]);
  const [classesLoading, setClassesLoading] = useState(true);
  const [classesError, setClassesError] = useState<string | null>(null);

  const [myHolds, setMyHolds] = useState<MyHold[]>([]);
  const [myHoldsLoading, setMyHoldsLoading] = useState(false);

  const [isRefreshingAll, setIsRefreshingAll] = useState(false);

  // Fetch classes
  const fetchClasses = useCallback(async (isSilent = false) => {
    if (!isSilent) setClassesLoading(true);
    try {
      const data = await api.getClasses();
      setClasses(data);
      setClassesError(null);
    } catch (err: any) {
      if (!isSilent) {
        setClassesError(err.message || 'Failed to connect to backend server.');
      }
    } finally {
      if (!isSilent) setClassesLoading(false);
    }
  }, []);

  // Fetch my holds
  const fetchMyHolds = useCallback(async (isSilent = false) => {
    if (!isStudentIdValid) {
      setMyHolds([]);
      return;
    }
    if (!isSilent) setMyHoldsLoading(true);
    try {
      const data = await api.getMyHolds(studentId);
      setMyHolds(data);
    } catch {
      // Ignore background poll errors for holds
    } finally {
      if (!isSilent) setMyHoldsLoading(false);
    }
  }, [studentId, isStudentIdValid]);

  // Combined refresh
  const handleRefreshAll = useCallback(async () => {
    setIsRefreshingAll(true);
    await Promise.all([fetchClasses(true), fetchMyHolds(true)]);
    setIsRefreshingAll(false);
  }, [fetchClasses, fetchMyHolds]);

  // Initial load & Polling for Classes (every 3 seconds)
  useEffect(() => {
    fetchClasses(false);
    const intervalId = setInterval(() => {
      fetchClasses(true);
    }, 3000);
    return () => clearInterval(intervalId);
  }, [fetchClasses]);

  // Fetch holds when studentId changes, and poll every 4 seconds
  useEffect(() => {
    fetchMyHolds(false);
    const intervalId = setInterval(() => {
      fetchMyHolds(true);
    }, 4000);
    return () => clearInterval(intervalId);
  }, [fetchMyHolds]);

  // Callback when a hold is created, confirmed, or cancelled
  const handleDataChanged = useCallback(() => {
    fetchClasses(true);
    fetchMyHolds(true);
  }, [fetchClasses, fetchMyHolds]);

  // Identify active hold for banner
  const activeHold = useMemo(() => {
    return myHolds.find((h) => h.status === 'HELD') || null;
  }, [myHolds]);

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800 flex flex-col">
      {/* Top Header & Student Identity Bar */}
      <Header
        studentId={studentId}
        onStudentIdChange={setStudentId}
        isValid={isStudentIdValid}
        validationError={validationError}
        onRefreshAll={handleRefreshAll}
        isRefreshing={isRefreshingAll}
      />

      {/* Main Container */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex-1 w-full">
        {/* Invalid Student ID Notice */}
        {!isStudentIdValid && (
          <div className="mb-6 p-4 bg-amber-50 border border-amber-300 rounded-xl flex items-start gap-3 shadow-xs">
            <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
            <div>
              <h4 className="text-sm font-bold text-amber-900">
                Action Required: Enter a Valid 17-Digit Student ID
              </h4>
              <p className="text-xs text-amber-800 mt-0.5">
                Each seat reservation and admission fee confirmation is strictly bound to the student birth registration number.
                Please ensure exactly 17 numeric digits are entered in the header above or click one of the quick presets.
              </p>
            </div>
          </div>
        )}

        {/* Section B: Application Details / Active Hold Card */}
        {activeHold && (
          <ActiveHoldBanner
            hold={activeHold}
            studentId={studentId}
            onHoldUpdated={handleDataChanged}
          />
        )}

        {/* Section D: Admin Class Creation */}
        <AdminCreateClass onClassCreated={handleDataChanged} />

        {/* Section A: Main Admission Dashboard & Class List */}
        <ClassList
          classes={classes}
          loading={classesLoading}
          error={classesError}
          studentId={studentId}
          isStudentIdValid={isStudentIdValid}
          myHolds={myHolds}
          onHoldCreated={handleDataChanged}
        />

        {/* Section C: My Applications */}
        <MyApplications
          holds={myHolds}
          studentId={studentId}
          loading={myHoldsLoading}
          onRefresh={() => fetchMyHolds(false)}
        />
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-slate-200 py-6 mt-12 text-center text-xs text-slate-400">
        <div className="max-w-7xl mx-auto px-4">
          <p className="font-medium text-slate-600">
            Saint Jude International School Admission Management System
          </p>
          <p className="mt-1 text-[11px] text-slate-400">
            High-concurrency transactional seat allocation powered by PostgreSQL row locks &amp; ASP.NET Core 8 Web API.
          </p>
        </div>
      </footer>
    </div>
  );
}

export default App;
