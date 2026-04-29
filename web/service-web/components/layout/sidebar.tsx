"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell,
  BookOpen,
  CircleDollarSign,
  LayoutDashboard,
  Megaphone,
  Package,
  Settings,
  TrendingUp,
} from "lucide-react";

import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/creator/dashboard", label: "홈", icon: LayoutDashboard },
  { href: "/creator/books", label: "책", icon: BookOpen },
  { href: "/creator/assets", label: "에셋", icon: Package },
  { href: "/creator/ads", label: "광고", icon: Megaphone },
  { href: "/creator/stats", label: "통계", icon: TrendingUp },
  { href: "/creator/settlements", label: "정산", icon: CircleDollarSign },
  { href: "/creator/notifications", label: "알림", icon: Bell },
  { href: "/creator/settings", label: "설정", icon: Settings },
];

export function Sidebar() {
  const pathname = usePathname();
  return (
    <aside className="hidden md:flex w-60 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground">
      <div className="px-5 py-5">
        <Link href="/creator/dashboard" className="flex items-center gap-2">
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground text-sm font-semibold">
            D
          </span>
          <span className="text-base font-semibold tracking-tight">
            dotshelf
          </span>
          <span className="ml-1 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
            크리에이터
          </span>
        </Link>
      </div>

      <nav className="flex flex-1 flex-col gap-0.5 px-3 pb-4">
        {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
          const active =
            pathname === href ||
            (href !== "/creator/dashboard" && pathname.startsWith(href));
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                active
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground",
              )}
            >
              <Icon className="size-4" />
              {label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-sidebar-border px-4 py-3 text-xs text-muted-foreground">
        v1.0 · 정산은 매주 화요일
      </div>
    </aside>
  );
}
