"use client";

import { useEffect, useState, type ReactNode } from "react";

/** mock 활성 여부. 운영 빌드에서는 NEXT_PUBLIC_USE_MOCKS 미설정 시 자동 비활성. */
const USE_MOCKS = process.env.NEXT_PUBLIC_USE_MOCKS === "true";

let mocksReady: Promise<void> | null = null;

async function ensureMocks() {
  if (typeof window === "undefined") return;
  if (mocksReady) return mocksReady;
  mocksReady = (async () => {
    // 첫 등록인 경우 service worker가 아직 페이지를 컨트롤하지 않아
    // 첫 fetch가 가로채지지 않는다. start() 후 controller가 잡히지 않으면 1회 reload.
    const wasControlled = !!navigator.serviceWorker?.controller;
    const { worker } = await import("@/mocks/browser");
    await worker.start({
      onUnhandledRequest: "bypass",
      serviceWorker: { url: "/mockServiceWorker.js" },
    });
    if (!wasControlled && !navigator.serviceWorker.controller) {
      window.location.reload();
      // reload 직전이라 children 렌더 막기 위해 영원히 pending
      await new Promise<never>(() => {});
    }
  })();
  return mocksReady;
}

export function MSWProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(!USE_MOCKS);
  useEffect(() => {
    if (!USE_MOCKS) return;
    ensureMocks().then(() => setReady(true));
  }, []);
  if (!ready) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background text-muted-foreground text-sm">
        준비 중…
      </div>
    );
  }
  return <>{children}</>;
}
