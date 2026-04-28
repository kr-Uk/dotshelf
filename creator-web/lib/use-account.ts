"use client";

import { useSyncExternalStore } from "react";

import {
  AUTH_CHANGED_EVENT_NAME,
  loadAuth,
  type AuthState,
} from "./auth";

/**
 * 현재 로그인 세션을 구독.
 * sessionStorage 기반 인증 상태가 바뀌면 자동 리렌더 (`storage` 이벤트 +
 * `dotshelf:auth-changed` 커스텀 이벤트 둘 다 구독).
 */
export function useAccount(): AuthState | null {
  return useSyncExternalStore(
    (notify) => {
      if (typeof window === "undefined") return () => {};
      const onStorage = (e: StorageEvent) => {
        if (!e.key || e.key.startsWith("dotshelf")) notify();
      };
      window.addEventListener("storage", onStorage);
      window.addEventListener(AUTH_CHANGED_EVENT_NAME, notify);
      return () => {
        window.removeEventListener("storage", onStorage);
        window.removeEventListener(AUTH_CHANGED_EVENT_NAME, notify);
      };
    },
    () => loadAuth(),
    () => null,
  );
}
