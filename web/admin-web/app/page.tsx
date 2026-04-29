"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

import { loadAuth } from "@/lib/auth";

export default function RootRedirect() {
  const router = useRouter();
  useEffect(() => {
    const auth = loadAuth();
    router.replace(auth ? "/dashboard" : "/login");
  }, [router]);
  return (
    <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">
      이동 중…
    </div>
  );
}
