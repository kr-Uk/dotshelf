"use client";

import { useSyncExternalStore } from "react";

/**
 * 클라이언트 마운트 여부.
 * `useState(false) + useEffect(() => setState(true))` 패턴이 React 19의
 * `react-hooks/set-state-in-effect` 룰에 걸려서, useSyncExternalStore로 대체.
 */
export function useMounted(): boolean {
  return useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );
}
