"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Clock } from "lucide-react";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import {
  clearAuth,
  loadAuth,
  SESSION_IDLE_TIMEOUT_MIN,
  touchAuth,
} from "@/lib/auth";

/** 비활성 30분 정책. 25분 시점에 경고, 30분 도달 시 강제 로그아웃. */
const WARN_AT_MIN = SESSION_IDLE_TIMEOUT_MIN - 5;
const TICK_MS = 15_000;

export function SessionWatcher() {
  const router = useRouter();
  const [warnOpen, setWarnOpen] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const dismissedAtRef = useRef<number | null>(null);

  const idleMin = useCallback(() => {
    const auth = loadAuth();
    if (!auth) return null;
    return (Date.now() - auth.lastActiveAt) / 60_000;
  }, []);

  // 사용자 활동(클릭·키 입력·휠)이 있으면 lastActiveAt 갱신.
  useEffect(() => {
    const onActivity = () => {
      // 경고가 떠있는 동안에는 자동 갱신하지 않는다 — 사용자가 「유지」를 명시적으로 눌러야 함.
      if (warnOpen) return;
      touchAuth();
    };
    const events = ["click", "keydown", "scroll", "mousemove"];
    events.forEach((ev) => window.addEventListener(ev, onActivity, { passive: true }));
    return () => events.forEach((ev) => window.removeEventListener(ev, onActivity));
  }, [warnOpen]);

  // 주기 체크 — 경고 띄우기 / 강제 로그아웃.
  useEffect(() => {
    let alive = true;
    const tick = () => {
      if (!alive) return;
      const m = idleMin();
      if (m === null) return;
      if (m >= SESSION_IDLE_TIMEOUT_MIN) {
        clearAuth();
        router.replace("/login");
        return;
      }
      if (m >= WARN_AT_MIN && !warnOpen && dismissedAtRef.current !== null) {
        // 한 번 dismiss했어도 시간이 다시 흘러 만료 임박 구간에 다시 진입하면 경고 재호출.
        dismissedAtRef.current = null;
      }
      if (m >= WARN_AT_MIN && !warnOpen && dismissedAtRef.current === null) {
        setWarnOpen(true);
      }
      if (warnOpen) {
        const left = Math.max(
          0,
          Math.round((SESSION_IDLE_TIMEOUT_MIN - m) * 60),
        );
        setSecondsLeft(left);
      }
    };
    tick();
    const id = setInterval(tick, TICK_MS);
    return () => {
      alive = false;
      clearInterval(id);
    };
  }, [idleMin, warnOpen, router]);

  // 모달이 떠있는 동안에는 1초마다 카운트다운 갱신.
  useEffect(() => {
    if (!warnOpen) return;
    const id = setInterval(() => {
      const m = idleMin();
      if (m === null) return;
      if (m >= SESSION_IDLE_TIMEOUT_MIN) {
        setWarnOpen(false);
        clearAuth();
        router.replace("/login");
        return;
      }
      setSecondsLeft(
        Math.max(0, Math.round((SESSION_IDLE_TIMEOUT_MIN - m) * 60)),
      );
    }, 1_000);
    return () => clearInterval(id);
  }, [warnOpen, idleMin, router]);

  const stay = () => {
    touchAuth();
    setWarnOpen(false);
    dismissedAtRef.current = Date.now();
  };

  const signOutNow = () => {
    setWarnOpen(false);
    clearAuth();
    router.replace("/login");
  };

  const mm = Math.floor(secondsLeft / 60);
  const ss = secondsLeft % 60;

  return (
    <Dialog open={warnOpen} onOpenChange={(open) => (open ? null : stay())}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Clock className="size-4 text-amber-600" />
            곧 자동 로그아웃됩니다
          </DialogTitle>
          <DialogDescription>
            비활성 {SESSION_IDLE_TIMEOUT_MIN}분 정책에 따라 세션이 곧 만료돼요.
            계속 작업하려면 「유지」를 눌러주세요.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col items-center gap-1 py-2">
          <div className="text-3xl font-semibold tabular-nums">
            {String(mm).padStart(1, "0")}:{String(ss).padStart(2, "0")}
          </div>
          <div className="text-xs text-muted-foreground">남음</div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={signOutNow}>
            지금 로그아웃
          </Button>
          <Button onClick={stay}>세션 유지</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
