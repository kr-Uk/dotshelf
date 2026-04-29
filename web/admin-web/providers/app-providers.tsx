"use client";

import type { ReactNode } from "react";
import { Toaster } from "@/components/ui/sonner";

import { MSWProvider } from "./msw-provider";
import { QueryProvider } from "./query-provider";
import { ThemeProvider } from "./theme-provider";

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider>
      <MSWProvider>
        <QueryProvider>
          {children}
          <Toaster richColors position="top-right" />
        </QueryProvider>
      </MSWProvider>
    </ThemeProvider>
  );
}
