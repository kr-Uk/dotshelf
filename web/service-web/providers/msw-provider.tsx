"use client";

import { useEffect, type ReactNode } from "react";

/** mock 활성 여부. 운영 빌드에서는 NEXT_PUBLIC_USE_MOCKS 미설정 시 자동 비활성. */
const USE_MOCKS = process.env.NEXT_PUBLIC_USE_MOCKS === "true";

let mocksReady: Promise<void> | null = null;

async function ensureMocks() {
  if (typeof window === "undefined") return;
  if (mocksReady) return mocksReady;
  mocksReady = (async () => {
    const { worker } = await import("@/mocks/browser");
    await worker.start({
      onUnhandledRequest: "bypass",
      serviceWorker: { url: "/mockServiceWorker.js" },
    });
  })();
  return mocksReady;
}

export function MSWProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    if (!USE_MOCKS) return;
    ensureMocks();
  }, []);
  return <>{children}</>;
}
