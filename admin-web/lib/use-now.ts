"use client";

import { useEffect, useState } from "react";

/**
 * 현재 시각(ms epoch)을 일정 간격으로 갱신하면서 반환.
 * `Date.now()`를 render 중에 직접 호출하지 않기 위한 hook.
 *
 * SLA 카운트다운, 잠금 만료 시각 비교 등 시간 의존 컴포넌트에서 사용.
 */
export function useNow(intervalMs = 1_000): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);

  return now;
}
