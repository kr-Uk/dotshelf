"use client";

import type { Account } from "./types";

/**
 * 인증 세션 보관소.
 *
 * ⚠️  운영(prod) 적용 시 교체:
 *   - 현재는 mock(MSW) 환경이라 서버가 Set-Cookie를 내려줄 수 없어,
 *     XSS 표면을 줄이기 위한 절충으로 sessionStorage를 사용한다 (탭 종료 시 소실).
 *   - 운영에서는 백엔드가 `Set-Cookie: HttpOnly; Secure; SameSite=Strict`
 *     쿠키로 세션을 발급하고, 클라이언트는 토큰을 읽지 않는다.
 *     `loadAuth`/`saveAuth`/`clearAuth` 인터페이스만 유지하면 호출부 변경 없이
 *     쿠키 기반 흐름으로 교체 가능.
 */
const STORAGE_KEY = "dotshelf:auth";
const SESSION_TIMEOUT_MIN = 30;
const AUTH_CHANGED_EVENT = "dotshelf:auth-changed";

export interface AuthState {
  token: string;
  account: Account;
  /** ms epoch */
  signedInAt: number;
  /** ms epoch — 마지막 활동 시각 */
  lastActiveAt: number;
}

function readStorage(): Storage | null {
  if (typeof window === "undefined") return null;
  return window.sessionStorage;
}

export function loadAuth(): AuthState | null {
  const store = readStorage();
  if (!store) return null;
  const raw = store.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    const state = JSON.parse(raw) as AuthState;
    const idleMin = (Date.now() - state.lastActiveAt) / 60_000;
    if (idleMin > SESSION_TIMEOUT_MIN) {
      store.removeItem(STORAGE_KEY);
      return null;
    }
    return state;
  } catch {
    return null;
  }
}

export function saveAuth(
  state: Pick<AuthState, "token" | "account">,
): AuthState {
  const store = readStorage();
  const now = Date.now();
  const full: AuthState = {
    ...state,
    signedInAt: now,
    lastActiveAt: now,
  };
  if (store) {
    store.setItem(STORAGE_KEY, JSON.stringify(full));
    window.dispatchEvent(new CustomEvent(AUTH_CHANGED_EVENT, { detail: full }));
  }
  return full;
}

export function touchAuth(): void {
  const store = readStorage();
  if (!store) return;
  const raw = store.getItem(STORAGE_KEY);
  if (!raw) return;
  try {
    const state = JSON.parse(raw) as AuthState;
    state.lastActiveAt = Date.now();
    store.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // ignore — 다음 loadAuth가 정리.
  }
}

export function clearAuth(): void {
  const store = readStorage();
  if (!store) return;
  store.removeItem(STORAGE_KEY);
  window.dispatchEvent(new CustomEvent(AUTH_CHANGED_EVENT));
}

export const SESSION_IDLE_TIMEOUT_MIN = SESSION_TIMEOUT_MIN;
export const AUTH_CHANGED_EVENT_NAME = AUTH_CHANGED_EVENT;
