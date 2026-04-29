"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

import { useAccount } from "@/lib/use-account";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const auth = useAccount();

  useEffect(() => {
    if (!auth) router.replace("/creator/login");
  }, [auth, router]);

  if (!auth) {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">
        인증 확인 중…
      </div>
    );
  }
  return <>{children}</>;
}
