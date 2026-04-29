"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ClipboardCheck,
  Coins,
  FileSearch,
  Flag,
  LayoutDashboard,
  Megaphone,
  ShieldCheck,
  TrendingUp,
  UserCog,
  UsersRound,
} from "lucide-react";

import { cn } from "@/lib/utils";
import { canRoleSeeNav, type NavKey } from "@/lib/permissions";
import { useAdminAccount } from "@/lib/use-admin-account";

const NAV_ITEMS: { key: NavKey; href: string; label: string; icon: typeof LayoutDashboard }[] = [
  { key: "dashboard", href: "/dashboard", label: "홈", icon: LayoutDashboard },
  { key: "reviews", href: "/reviews", label: "심사", icon: ClipboardCheck },
  { key: "signups", href: "/signups", label: "가입", icon: UserCog },
  { key: "moderation", href: "/moderation", label: "모더레이션", icon: Flag },
  { key: "settlements", href: "/settlements", label: "정산", icon: Coins },
  { key: "users", href: "/users", label: "사용자·공간", icon: UsersRound },
  { key: "stats", href: "/stats", label: "통계", icon: TrendingUp },
  { key: "operations", href: "/operations", label: "운영", icon: Megaphone },
  { key: "audit", href: "/audit", label: "감사 로그", icon: FileSearch },
];

export function Sidebar() {
  const pathname = usePathname();
  const auth = useAdminAccount();
  const role = auth?.account.role;
  const items = NAV_ITEMS.filter((it) => canRoleSeeNav(role, it.key));
  return (
    <aside className="flex w-60 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground">
      <div className="px-5 py-5">
        <Link href="/dashboard" className="flex items-center gap-2">
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground text-sm font-semibold">
            D
          </span>
          <span className="text-base font-semibold tracking-tight">
            dotshelf
          </span>
          <span className="ml-1 inline-flex items-center gap-1 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground">
            <ShieldCheck className="size-3" /> 어드민
          </span>
        </Link>
      </div>

      <nav className="flex flex-1 flex-col gap-0.5 px-3 pb-4">
        {items.map(({ href, label, icon: Icon }) => {
          const active =
            pathname === href ||
            (href !== "/dashboard" && pathname.startsWith(href));
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

      <div className="border-t border-sidebar-border px-4 py-3 text-[11px] leading-relaxed text-muted-foreground">
        v1.0 · 사내 SSO + MFA · 비활성 30분 자동 로그아웃
      </div>
    </aside>
  );
}
