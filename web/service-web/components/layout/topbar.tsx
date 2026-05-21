"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bell, ChevronDown, LogOut, Settings as SettingsIcon } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Badge } from "@/components/ui/badge";
import { clearAuth } from "@/lib/auth";
import { useAccount } from "@/lib/use-account";
import { SUPPLIER_TYPE_LABEL } from "@/lib/types";

export function Topbar() {
  const router = useRouter();
  const auth = useAccount();

  const handleLogout = () => {
    clearAuth();
    router.replace("/creator/login");
  };

  const supplierTypes = auth?.account.supplierTypes ?? [];

  return (
    <header className="flex h-14 items-center justify-between border-b border-border bg-background/80 px-4 backdrop-blur md:px-6">
      <div />

      <div className="flex items-center gap-2">
        <Link
          href="/creator/notifications"
          aria-label="알림"
          className="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-muted"
        >
          <Bell className="size-4" />
        </Link>

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button variant="ghost" className="gap-2 px-2">
                <span className="inline-flex size-7 items-center justify-center rounded-full bg-muted text-xs font-semibold">
                  {auth?.account.businessName?.[0] ?? "D"}
                </span>
                <span className="hidden text-sm md:inline">
                  {auth?.account.businessName ?? "게스트"}
                </span>
                <ChevronDown className="size-3.5 text-muted-foreground" />
              </Button>
            }
          />
          <DropdownMenuContent align="end" className="w-64">
            <div className="grid gap-1.5 px-2 py-2">
              <span className="text-sm font-semibold">
                {auth?.account.businessName ?? "게스트"}
              </span>
              <span className="text-xs text-muted-foreground">
                {auth?.account.email ?? "—"}
              </span>
              <div className="flex flex-wrap gap-1 pt-1">
                {supplierTypes.map((t) => (
                  <Badge key={t} variant="secondary" className="text-[10px]">
                    {SUPPLIER_TYPE_LABEL[t]}
                  </Badge>
                ))}
              </div>
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => router.push("/creator/settings")}>
              <SettingsIcon className="size-4" /> 설정
            </DropdownMenuItem>
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
