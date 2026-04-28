"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";

import { canRoleSeeNav, type NavKey } from "@/lib/permissions";
import { useAdminAccount } from "@/lib/use-admin-account";

const PATH_TO_NAV: Array<[string, NavKey]> = [
  ["/dashboard", "dashboard"],
  ["/reviews", "reviews"],
  ["/signups", "signups"],
  ["/moderation", "moderation"],
  ["/settlements", "settlements"],
  ["/users", "users"],
  ["/stats", "stats"],
  ["/operations", "operations"],
  ["/audit", "audit"],
];

function navKeyForPath(pathname: string): NavKey | null {
  for (const [prefix, key] of PATH_TO_NAV) {
    if (pathname === prefix || pathname.startsWith(prefix + "/")) return key;
  }
  return null;
}

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const auth = useAdminAccount();

  // 비로그인 → /login, 권한 없는 페이지 → /dashboard 리다이렉트.
  // useSyncExternalStore가 첫 렌더부터 실제 storage 값을 보장하므로,
  // 별도의 "checked" 플래그 없이 effect 한 번이면 충분.
  useEffect(() => {
    if (!auth) {
      router.replace("/login");
      return;
    }
    const navKey = navKeyForPath(pathname);
    if (navKey && !canRoleSeeNav(auth.account.role, navKey)) {
      router.replace("/dashboard");
    }
  }, [auth, pathname, router]);

  if (!auth) {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">
        세션 확인 중…
      </div>
    );
  }
  return <>{children}</>;
}
