"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";

import { canRoleSeeNav, type NavKey } from "@/lib/permissions";
import { useAdminAccount } from "@/lib/use-admin-account";
import { useMounted } from "@/lib/use-mounted";

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
  const mounted = useMounted();
  const auth = useAdminAccount();

  // 비로그인 → /login, 권한 없는 페이지 → /dashboard 리다이렉트.
  // hydration 첫 렌더에서는 server snapshot(null)이 올 수 있으므로
  // client mount 이후 실제 sessionStorage 값을 확인한 뒤 이동한다.
  useEffect(() => {
    if (!mounted) return;
    if (auth === undefined) return;
    if (!auth) {
      router.replace("/login");
      return;
    }
    const navKey = navKeyForPath(pathname);
    if (navKey && !canRoleSeeNav(auth.account.role, navKey)) {
      router.replace("/dashboard");
    }
  }, [auth, mounted, pathname, router]);

  if (!mounted || auth === undefined || !auth) {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">
        세션 확인 중…
      </div>
    );
  }
  return <>{children}</>;
}
