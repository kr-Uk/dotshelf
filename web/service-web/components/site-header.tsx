import Link from "next/link";

import { SiteNav } from "@/components/site-nav";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-border bg-background/80 px-6 backdrop-blur">
      <Link href="/" className="flex items-center gap-2">
        <span className="inline-flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground text-sm font-semibold">
          D
        </span>
        <span className="text-base font-semibold tracking-tight">dotshelf</span>
      </Link>
      <SiteNav />
    </header>
  );
}
