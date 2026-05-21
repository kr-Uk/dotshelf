"use client";

import { loadAuth, touchAuth } from "./auth";

/**
 * REST 클라이언트 — 두 앱(`admin-web`, `creator-web`)이 동일한 모양으로 유지.
 *
 * 운영 응답 envelope 표준 (회원가입 명세 예시 참조):
 *   { httpStatus, isSuccess, message, code, result }
 *
 * 현재 mock(MSW) 핸들러는 raw 데이터 반환과 envelope 반환이 혼재할 수 있다.
 * `api()`는 두 형태를 모두 받아주며, envelope이면 `result`만 unwrap해 호출부에 넘긴다.
 * 운영 백엔드는 envelope 강제 — 그 시점에 mock도 envelope으로 정렬.
 */

const API_BASE = "/api";

export interface ResponseEnvelope<T> {
  httpStatus: string;
  isSuccess: boolean;
  message: string;
  code: number;
  result: T | null;
}

class ApiError extends Error {
  status: number;
  code?: number;
  constructor(status: number, message: string, code?: number) {
    super(message);
    this.status = status;
    this.code = code;
    this.name = "ApiError";
  }
}

function isEnvelope(payload: unknown): payload is ResponseEnvelope<unknown> {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  return "isSuccess" in p && "code" in p && "result" in p;
}

export async function api<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const auth = loadAuth();
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (auth?.token) {
    headers.set("Authorization", `Bearer ${auth.token}`);
    touchAuth();
  }

  // 절대 URL은 그대로 사용, 그 외에는 /api prefix.
  const url = path.startsWith("http") ? path : `${API_BASE}${path}`;

  const res = await fetch(url, { ...init, headers });

  if (res.status === 204) return undefined as T;

  let payload: unknown = null;
  try {
    payload = await res.json();
  } catch {
    payload = null;
  }

  if (!res.ok) {
    if (isEnvelope(payload)) {
      throw new ApiError(res.status, payload.message, payload.code);
    }
    const message =
      payload && typeof (payload as { message?: unknown }).message === "string"
        ? (payload as { message: string }).message
        : res.statusText;
    throw new ApiError(res.status, message);
  }

  if (isEnvelope(payload)) {
    if (!payload.isSuccess) {
      throw new ApiError(res.status, payload.message, payload.code);
    }
    return (payload.result ?? undefined) as T;
  }
  return payload as T;
}

export { ApiError };
