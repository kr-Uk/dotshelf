"use client";

import { useSyncExternalStore } from "react";

import {
  AUTH_CHANGED_EVENT_NAME,
  loadAuth,
  saveAuth,
  type AuthState,
} from "./auth";
import type { AdminRole } from "./types";

/**
 * 현재 관리자 세션을 구독.
 * sessionStorage 기반 인증 상태가 바뀌면 자동 리렌더 (`storage` 이벤트 +
 * `dotshelf-admin:auth-changed` 커스텀 이벤트 둘 다 구독).
 */
export function useAdminAccount(): AuthState | null {
  return useSyncExternalStore(
    (notify) => {
      if (typeof window === "undefined") return () => {};
      const onStorage = (e: StorageEvent) => {
        if (!e.key || e.key.startsWith("dotshelf-admin")) notify();
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

/** 데모용 — 현재 세션의 role만 즉시 바꿔서 RBAC 가시성을 시연. */
export function switchRoleForDemo(role: AdminRole): void {
  const auth = loadAuth();
  if (!auth) return;
  saveAuth({
    token: auth.token,
    account: { ...auth.account, role },
  });
}
