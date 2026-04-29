"use client";

import { useRouter } from "next/navigation";
import { ChevronDown, FlaskConical, LogOut, ShieldCheck } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Badge } from "@/components/ui/badge";
import { GlobalSearch } from "@/components/layout/global-search";
import { NotificationBell } from "@/components/layout/notification-bell";
import { ThemeToggle } from "@/components/layout/theme-toggle";
import { clearAuth, SESSION_IDLE_TIMEOUT_MIN } from "@/lib/auth";
import { ADMIN_ROLE_LABEL, type AdminRole } from "@/lib/types";
import { useAdminAccount, switchRoleForDemo } from "@/lib/use-admin-account";

const DEMO_ROLES: AdminRole[] = [
  "super_admin",
  "content_reviewer",
  "moderator",
  "settlement",
  "cs",
  "read_only",
];

export function Topbar() {
  const router = useRouter();
  const auth = useAdminAccount();

  const handleLogout = () => {
    clearAuth();
    router.replace("/login");
  };

  return (
    <header className="flex h-14 items-center gap-4 border-b border-border bg-background/80 px-4 backdrop-blur md:px-6">
      <GlobalSearch />

      <div className="flex items-center gap-2">
        <Badge variant="outline" className="hidden gap-1 md:inline-flex">
          <ShieldCheck className="size-3" /> IP {auth?.account.ipAllowlisted ? "허용" : "외부"}
        </Badge>

        <ThemeToggle />
        <NotificationBell />

        {/* 데모용 — 권한별 차등 노출을 즉시 시연 */}
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button variant="outline" size="sm" className="gap-1.5">
                <FlaskConical className="size-3.5" />
                <span className="hidden md:inline">
                  {auth?.account.role ? ADMIN_ROLE_LABEL[auth.account.role] : "역할"}
                </span>
                <ChevronDown className="size-3.5 text-muted-foreground" />
              </Button>
            }
          />
          <DropdownMenuContent align="end" className="w-56">
            <div className="flex items-center gap-1.5 px-2 py-1 text-xs text-muted-foreground">
              <FlaskConical className="size-3" /> 데모 — 역할 전환
            </div>
            <DropdownMenuRadioGroup
              value={auth?.account.role ?? "super_admin"}
              onValueChange={(v) => switchRoleForDemo((v ?? "super_admin") as AdminRole)}
            >
              {DEMO_ROLES.map((r) => (
                <DropdownMenuRadioItem key={r} value={r}>
                  {ADMIN_ROLE_LABEL[r]}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <div className="px-2 py-1 text-[11px] leading-relaxed text-muted-foreground">
              실제 운영에서는 IdP가 부여하는 역할만 허용됩니다.
            </div>
          </DropdownMenuContent>
        </DropdownMenu>

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button variant="ghost" className="gap-2 px-2">
                <span className="inline-flex size-7 items-center justify-center rounded-full bg-muted text-xs font-semibold">
                  {auth?.account.name?.[0] ?? "어"}
                </span>
                <span className="hidden text-sm md:inline">
                  {auth?.account.name ?? "관리자"}
                </span>
                <ChevronDown className="size-3.5 text-muted-foreground" />
              </Button>
            }
          />
          <DropdownMenuContent align="end" className="w-72">
            <div className="grid gap-1.5 px-2 py-2">
              <span className="text-sm font-semibold">
                {auth?.account.name ?? "관리자"}
              </span>
              <span className="text-xs text-muted-foreground">
                {auth?.account.email ?? "—"}
              </span>
              <div className="flex flex-wrap gap-1 pt-1">
                {auth?.account.role && (
                  <Badge variant="secondary" className="text-[10px]">
                    {ADMIN_ROLE_LABEL[auth.account.role]}
                  </Badge>
                )}
                {auth?.account.team && (
                  <Badge variant="outline" className="text-[10px]">
                    {auth.account.team}
                  </Badge>
                )}
              </div>
            </div>
            <DropdownMenuSeparator />
            <div className="px-2 py-1 text-[11px] leading-relaxed text-muted-foreground">
              비활성 {SESSION_IDLE_TIMEOUT_MIN}분이 지나면 자동 로그아웃됩니다.
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleLogout}>
              <LogOut className="size-4" /> 로그아웃
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
