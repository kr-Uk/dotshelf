"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";

import { Input } from "@/components/ui/input";
import { Kbd } from "@/components/common/kbd";
import { api } from "@/lib/api";
import { useCommandK } from "@/lib/use-keyboard-shortcut";
import { useMounted } from "@/lib/use-mounted";
import { SEARCH_HIT_LABEL, type SearchHit } from "@/lib/types";
import { cn } from "@/lib/utils";

function detectMac(): boolean {
  if (typeof navigator === "undefined") return true;
  return /Mac|iPhone|iPad|iPod/.test(navigator.platform);
}

const TYPE_TONE: Record<SearchHit["type"], string> = {
  user: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300",
  signup: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300",
  review: "bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300",
  report:
    "bg-destructive/10 text-destructive dark:bg-destructive/20 dark:text-red-300",
};

export function GlobalSearch() {
  const router = useRouter();
  const [q, setQ] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  // SSR과 첫 paint에서는 mac 가정 — mount 후 실제 platform으로 결정.
  const mounted = useMounted();
  const isMac = mounted ? detectMac() : true;

  useCommandK(() => {
    inputRef.current?.focus();
    inputRef.current?.select();
    setOpen(true);
  });

  useEffect(() => {
    const t = setTimeout(() => setDebounced(q), 180);
    return () => clearTimeout(t);
  }, [q]);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    window.addEventListener("mousedown", onClickOutside);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("mousedown", onClickOutside);
      window.removeEventListener("keydown", onKey);
    };
  }, []);

  const { data, isFetching } = useQuery({
    queryKey: ["global-search", debounced],
    queryFn: () => api<SearchHit[]>(`/search?q=${encodeURIComponent(debounced)}`),
    enabled: debounced.trim().length >= 1,
    staleTime: 30_000,
  });

  const results = data ?? [];
  const showPanel = open && q.trim().length > 0;

  return (
    <div ref={wrapperRef} className="relative flex max-w-xl flex-1 items-center gap-2">
      <Search className="size-4 text-muted-foreground" />
      <Input
        ref={inputRef}
        placeholder="콘텐츠 ID · 닉네임 · 이메일 · 사업자번호 검색"
        className="h-8 border-none bg-transparent pr-16 shadow-none focus-visible:ring-0 focus-visible:border-none"
        value={q}
        onChange={(e) => {
          setQ(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && results[0]) {
            router.push(results[0].href);
            setOpen(false);
          }
        }}
      />
      <div className="pointer-events-none absolute right-1 top-1/2 hidden -translate-y-1/2 gap-1 md:flex">
        <Kbd>{isMac ? "⌘" : "Ctrl"}</Kbd>
        <Kbd>K</Kbd>
      </div>

      {showPanel && (
        <div className="absolute left-7 right-0 top-full z-30 mt-1 max-h-96 overflow-y-auto rounded-lg border border-border bg-popover p-1 shadow-lg ring-1 ring-foreground/10">
          {isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-muted-foreground">검색 중…</div>
          )}
          {!isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-muted-foreground">
              일치하는 결과가 없어요. ID·이메일·닉네임·사업자번호·콘텐츠 제목으로 시도해보세요.
            </div>
          )}
          {results.map((hit) => (
            <Link
              key={`${hit.type}-${hit.id}`}
              href={hit.href}
              onClick={() => setOpen(false)}
              className="flex items-start gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground"
            >
              <span
                className={cn(
                  "mt-0.5 inline-flex h-5 shrink-0 items-center rounded-full px-1.5 text-[10px] font-medium",
                  TYPE_TONE[hit.type],
                )}
              >
                {SEARCH_HIT_LABEL[hit.type]}
              </span>
              <div className="min-w-0 flex-1">
                <div className="line-clamp-1 font-medium">{hit.title}</div>
                <div className="line-clamp-1 text-xs text-muted-foreground">
                  {hit.subtitle}
                </div>
              </div>
            </Link>
          ))}
          {results.length > 0 && (
            <div className="border-t px-3 py-1.5 text-[10px] text-muted-foreground">
              Enter로 첫 결과 열기 · Esc로 닫기
            </div>
          )}
        </div>
      )}
    </div>
  );
}
