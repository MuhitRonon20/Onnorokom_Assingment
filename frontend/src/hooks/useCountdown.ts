import { useState, useEffect, useRef } from 'react';

export function calculateRemainingSeconds(expiresAt?: string): number {
  if (!expiresAt) return 0;
  const target = new Date(expiresAt).getTime();
  const now = Date.now();
  const diff = Math.floor((target - now) / 1000);
  return Math.max(0, diff);
}

export function formatSeconds(totalSeconds: number): string {
  if (totalSeconds <= 0) return '00:00';
  const mins = Math.floor(totalSeconds / 60);
  const secs = totalSeconds % 60;
  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

export function useCountdown(expiresAt?: string, onExpire?: () => void) {
  const [remainingSeconds, setRemainingSeconds] = useState<number>(() =>
    calculateRemainingSeconds(expiresAt)
  );
  const onExpireRef = useRef(onExpire);
  onExpireRef.current = onExpire;
  const hasTriggeredRef = useRef(false);

  useEffect(() => {
    hasTriggeredRef.current = false;
    const initial = calculateRemainingSeconds(expiresAt);
    setRemainingSeconds(initial);

    if (initial <= 0 && expiresAt) {
      if (!hasTriggeredRef.current) {
        hasTriggeredRef.current = true;
        onExpireRef.current?.();
      }
      return;
    }

    const intervalId = setInterval(() => {
      const remaining = calculateRemainingSeconds(expiresAt);
      setRemainingSeconds(remaining);

      if (remaining <= 0) {
        clearInterval(intervalId);
        if (!hasTriggeredRef.current) {
          hasTriggeredRef.current = true;
          onExpireRef.current?.();
        }
      }
    }, 1000);

    return () => clearInterval(intervalId);
  }, [expiresAt]);

  return {
    remainingSeconds,
    formattedTime: formatSeconds(remainingSeconds),
    isExpired: remainingSeconds <= 0 && Boolean(expiresAt),
  };
}

