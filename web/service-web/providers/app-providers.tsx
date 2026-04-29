"use client";

import type { ReactNode } from "react";
import { Toaster } from "@/components/ui/sonner";

import { MSWProvider } from "./msw-provider";
import { QueryProvider } from "./query-provider";

/** 루트 전역에 항상 들어가는 가벼운 providers */
export function GlobalProviders({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      <Toaster richColors position="top-right" />
    </>
  );
}

/** API 호출이 필요한 영역(인증/앱)에서 wrapping. MSW + QueryClient. */
export function ApiProviders({ children }: { children: ReactNode }) {
  return (
    <MSWProvider>
      <QueryProvider>{children}</QueryProvider>
    </MSWProvider>
  );
}
