"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PackagePlus, Search } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import { formatDate, formatPages } from "@/lib/format";
import {
  ASSET_CATEGORY_LABEL,
  BOOK_STATUS_LABEL,
  type Asset,
  type AssetCategory,
  type AssetSaleStatus,
} from "@/lib/types";

type StatusFilter = AssetSaleStatus | "all";
type CategoryFilter = AssetCategory | "all";

const STATUS_OPTIONS: Array<{ value: StatusFilter; label: string }> = [
  { value: "all", label: "전체" },
  { value: "draft", label: BOOK_STATUS_LABEL.draft },
  { value: "submitted", label: BOOK_STATUS_LABEL.submitted },
  { value: "in_review", label: BOOK_STATUS_LABEL.in_review },
  { value: "rejected", label: BOOK_STATUS_LABEL.rejected },
  { value: "on_sale", label: BOOK_STATUS_LABEL.on_sale },
  { value: "off_sale", label: BOOK_STATUS_LABEL.off_sale },
];

const CATEGORY_OPTIONS: Array<{ value: CategoryFilter; label: string }> = [
  { value: "all", label: "전체 카테고리" },
  ...Object.entries(ASSET_CATEGORY_LABEL).map(([v, l]) => ({
    value: v as AssetCategory,
    label: l,
  })),
];

function statusBadge(status: AssetSaleStatus) {
  if (status === "on_sale")
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {BOOK_STATUS_LABEL.on_sale}
      </Badge>
    );
  if (status === "rejected")
    return <Badge variant="destructive">{BOOK_STATUS_LABEL.rejected}</Badge>;
  if (status === "in_review" || status === "submitted")
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {BOOK_STATUS_LABEL[status]}
      </Badge>
    );
  if (status === "off_sale")
    return (
      <Badge variant="outline" className="text-muted-foreground">
        {BOOK_STATUS_LABEL.off_sale}
      </Badge>
    );
  return <Badge variant="secondary">{BOOK_STATUS_LABEL[status]}</Badge>;
}

export default function AssetsPage() {
  const [status, setStatus] = useState<StatusFilter>("all");
  const [category, setCategory] = useState<CategoryFilter>("all");
  const [q, setQ] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: ["assets", status, category, q],
    queryFn: () => {
      const params = new URLSearchParams();
      if (status !== "all") params.set("status", status);
      if (category !== "all") params.set("category", category);
      if (q) params.set("q", q);
      const qs = params.toString();
      return api<Asset[]>(`/assets${qs ? `?${qs}` : ""}`);
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="text-xs font-medium text-muted-foreground">
            4. 커스텀 에셋 판매 신청
          </div>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">에셋</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            12개 카테고리 + 호환 타겟. 전체 테마는 다중 슬롯 의존이에요.
          </p>
        </div>
        <Button size="lg" render={<Link href="/assets/new" />}>
          <PackagePlus className="size-4" /> 신규 등록
        </Button>
      </div>

      <Card>
        <CardHeader className="gap-3">
          <div className="grid gap-3 md:grid-cols-[1fr_220px_180px]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="이름 검색 (한국어/영어)"
                className="pl-9"
              />
            </div>
            <Select
              value={category}
              onValueChange={(v) => setCategory(v as CategoryFilter)}
            >
              <SelectTrigger>
                <SelectValue placeholder="카테고리">
                  {(v: CategoryFilter) =>
                    CATEGORY_OPTIONS.find((o) => o.value === v)?.label ??
                    "카테고리"
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {CATEGORY_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={status}
              onValueChange={(v) => setStatus(v as StatusFilter)}
            >
              <SelectTrigger>
                <SelectValue placeholder="상태">
                  {(v: StatusFilter) =>
                    STATUS_OPTIONS.find((o) => o.value === v)?.label ?? "상태"
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-6">이름</TableHead>
                <TableHead>카테고리</TableHead>
                <TableHead>호환 타겟</TableHead>
                <TableHead>가격</TableHead>
                <TableHead>업데이트</TableHead>
                <TableHead className="pr-6 text-right">상태</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading &&
                [1, 2, 3].map((i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={6} className="px-6">
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                  </TableRow>
                ))}
              {data?.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="px-6 py-12 text-center text-sm text-muted-foreground"
                  >
                    조건에 맞는 에셋이 없어요.
                  </TableCell>
                </TableRow>
              )}
              {data?.map((a) => (
                <TableRow key={a.id} className="cursor-pointer">
                  <TableCell className="pl-6">
                    <Link
                      href={`/assets/${a.id}`}
                      className="font-medium hover:underline"
                    >
                      {a.name}
                    </Link>
                    {a.nameEn && (
                      <div className="text-xs text-muted-foreground">
                        {a.nameEn}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">
                      {ASSET_CATEGORY_LABEL[a.category]}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {a.category === "full_theme"
                      ? `다중 슬롯 ${Object.keys(a.themeSlots ?? {}).length}개`
                      : a.compatTargetIds.length > 0
                        ? `${a.compatTargetIds.length}개`
                        : "—"}
                  </TableCell>
                  <TableCell>{formatPages(a.pricePages)}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(a.updatedAt)}
                  </TableCell>
                  <TableCell className="pr-6 text-right">
                    {statusBadge(a.status)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">카테고리 가이드</CardTitle>
          <CardDescription>
            12종 카테고리 + 호환 타겟. 전체 테마는 책장·대시보드·벽지·바닥 슬롯이
            모두 첨부되어야 검수 통과해요.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-2 text-xs md:grid-cols-3">
          {Object.entries(ASSET_CATEGORY_LABEL).map(([k, l]) => (
            <div
              key={k}
              className="flex items-center gap-2 rounded-md border border-border bg-card px-2.5 py-1.5"
            >
              <Badge variant="outline" className="text-[10px]">
                {l}
              </Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
