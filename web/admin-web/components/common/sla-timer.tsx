"use client";

import { useEffect, useState } from "react";

import { StatusBadge } from "@/components/common/status-badge";

/** 1초마다 다시 그려서 카운트다운으로 보여준다. */
export function SlaTimer({ dueAt }: { dueAt: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(t);
  }, []);

  const due = new Date(dueAt).getTime();
  const diffMs = due - now;
  const passed = diffMs < 0;
  const abs = Math.abs(diffMs);
  const totalSec = Math.floor(abs / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;

  const text = passed
    ? h > 0
      ? `${h}h ${m}m 지남`
      : `${m}m ${String(s).padStart(2, "0")}s 지남`
    : h >= 24
      ? `${Math.floor(h / 24)}d ${h % 24}h 남음`
      : h > 0
        ? `${h}h ${m}m 남음`
        : `${m}m ${String(s).padStart(2, "0")}s 남음`;

  const tone =
    passed
      ? "danger"
      : h < 1
        ? "warn"
        : h < 12
          ? "info"
          : "neutral";

  return <StatusBadge tone={tone}>{text}</StatusBadge>;
}
