"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AlarmClock,
  AlertOctagon,
  Bell,
  CircleAlert,
  Coins,
  Flag,
  ServerCrash,
  ShieldAlert,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import {
  ADMIN_NOTIFICATION_LABEL,
  type AdminNotification,
  type AdminNotificationKind,
} from "@/lib/types";
import { formatRelative } from "@/lib/format";
import { cn } from "@/lib/utils";

const KIND_ICON: Record<AdminNotificationKind, React.ComponentType<{ className?: string }>> = {
  review_sla: AlarmClock,
  moderation_p1: Flag,
  settlement_mismatch: Coins,
  ip_violation: ShieldAlert,
  report_spike: AlertOctagon,
  system_health: ServerCrash,
};

const KIND_TONE: Record<AdminNotificationKind, string> = {
  review_sla: "text-amber-600",
  moderation_p1: "text-destructive",
  settlement_mismatch: "text-amber-600",
  ip_violation: "text-destructive",
  report_spike: "text-amber-600",
  system_health: "text-sky-600",
};

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const qc = useQueryClient();

  const { data } = useQuery({
    queryKey: ["admin-notifications"],
    queryFn: () => api<AdminNotification[]>("/notifications"),
  });

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    window.addEventListener("mousedown", onClickOutside);
    return () => window.removeEventListener("mousedown", onClickOutside);
  }, []);

  const unread = (data ?? []).filter((n) => !n.read).length;

  const readAll = async () => {
    try {
      await api("/notifications/read-all", { method: "POST" });
      qc.setQueryData<AdminNotification[]>(["admin-notifications"], (prev) =>
        prev?.map((n) => ({ ...n, read: true })) ?? prev,
      );
      toast.success("모든 알림 읽음 처리");
    } catch {
      toast.error("처리에 실패했어요.");
    }
  };

  return (
    <div ref={wrapperRef} className="relative">
      <Button
        variant="ghost"
        size="icon-sm"
        aria-label="알림"
        onClick={() => setOpen((v) => !v)}
        className="relative"
      >
        <Bell className="size-4" />
        {unread > 0 && (
          <span className="absolute right-1 top-1 inline-flex size-1.5 rounded-full bg-destructive" />
        )}
      </Button>

      {open && (
        <div className="absolute right-0 top-full z-30 mt-2 w-96 rounded-lg border border-border bg-popover p-1 shadow-lg ring-1 ring-foreground/10">
          <div className="flex items-center justify-between px-2 py-1.5">
            <div className="text-sm font-semibold">알림</div>
            {unread > 0 && (
              <Button size="xs" variant="ghost" onClick={readAll}>
                모두 읽음
              </Button>
            )}
          </div>
          <div className="max-h-[420px] overflow-y-auto">
            {(data ?? []).length === 0 && (
              <div className="px-3 py-6 text-center text-xs text-muted-foreground">
                받은 알림이 없어요.
              </div>
            )}
            {(data ?? []).map((n) => {
              const Icon = KIND_ICON[n.kind] ?? CircleAlert;
              const inner = (
                <div
                  className={cn(
                    "flex items-start gap-2 rounded-md px-2 py-2 text-sm hover:bg-accent",
                    !n.read && "bg-accent/40",
                  )}
                >
                  <Icon className={cn("mt-0.5 size-4 shrink-0", KIND_TONE[n.kind])} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                        {ADMIN_NOTIFICATION_LABEL[n.kind]}
                      </span>
                      <span className="text-[10px] text-muted-foreground">
                        · {formatRelative(n.createdAt)}
                      </span>
                      {!n.read && (
                        <span className="ml-auto inline-flex size-1.5 rounded-full bg-destructive" />
                      )}
                    </div>
                    <div className="mt-0.5 line-clamp-1 font-medium">{n.title}</div>
                    <div className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
                      {n.body}
                    </div>
                  </div>
                </div>
              );
              return n.href ? (
                <Link
                  key={n.id}
                  href={n.href}
                  onClick={() => setOpen(false)}
                  className="block"
                >
                  {inner}
                </Link>
              ) : (
                <div key={n.id}>{inner}</div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
