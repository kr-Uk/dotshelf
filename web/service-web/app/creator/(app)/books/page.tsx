"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { BookPlus, Search } from "lucide-react";

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
  BOOK_CATEGORY_LABEL,
  BOOK_STATUS_LABEL,
  type Book,
  type BookSaleStatus,
} from "@/lib/types";

type StatusFilter = BookSaleStatus | "all";

const STATUS_OPTIONS: Array<{ value: StatusFilter; label: string }> = [
  { value: "all", label: "전체" },
  { value: "draft", label: BOOK_STATUS_LABEL.draft },
  { value: "submitted", label: BOOK_STATUS_LABEL.submitted },
  { value: "in_review", label: BOOK_STATUS_LABEL.in_review },
  { value: "rejected", label: BOOK_STATUS_LABEL.rejected },
  { value: "on_sale", label: BOOK_STATUS_LABEL.on_sale },
  { value: "off_sale", label: BOOK_STATUS_LABEL.off_sale },
];

function statusBadge(status: BookSaleStatus) {
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

export default function BooksPage() {
  const [status, setStatus] = useState<StatusFilter>("all");
  const [q, setQ] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: ["books", status, q],
    queryFn: () => {
      const params = new URLSearchParams();
      if (status !== "all") params.set("status", status);
      if (q) params.set("q", q);
      const qs = params.toString();
      return api<Book[]>(`/books${qs ? `?${qs}` : ""}`);
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="text-xs font-medium text-muted-foreground">3. 책 등록</div>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">책</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            ISBN 매칭 → 권리 증빙 → 3D 표지 → 판매 정보. 검수 결과는 1.5영업일 내에.
          </p>
        </div>
        <Button size="lg" render={<Link href="/creator/books/new" />}>
          <BookPlus className="size-4" /> 신규 등록
        </Button>
      </div>

      <Card>
        <CardHeader className="gap-3">
          <div className="grid gap-3 md:grid-cols-[1fr_180px]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="제목·저자·ISBN 검색"
                className="pl-9"
              />
            </div>
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
                <TableHead className="pl-6">제목</TableHead>
                <TableHead>저자</TableHead>
                <TableHead>카테고리</TableHead>
                <TableHead>가격</TableHead>
                <TableHead>미리보기</TableHead>
                <TableHead>업데이트</TableHead>
                <TableHead className="pr-6 text-right">상태</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading &&
                [1, 2, 3].map((i) => (
                  <TableRow key={i}>
                    <TableCell colSpan={7} className="px-6">
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                  </TableRow>
                ))}
              {data?.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="px-6 py-12 text-center text-sm text-muted-foreground"
                  >
                    조건에 맞는 책이 없어요.
                  </TableCell>
                </TableRow>
              )}
              {data?.map((b) => (
                <TableRow key={b.id} className="cursor-pointer">
                  <TableCell className="pl-6">
                    <Link
                      href={`/creator/books/${b.id}`}
                      className="font-medium hover:underline"
                    >
                      {b.title}
                    </Link>
                    {b.isbn ? (
                      <div className="text-xs text-muted-foreground">
                        ISBN {b.isbn}
                      </div>
                    ) : (
                      <div className="text-xs text-muted-foreground">
                        자가출판
                      </div>
                    )}
                  </TableCell>
                  <TableCell>{b.author}</TableCell>
                  <TableCell>{BOOK_CATEGORY_LABEL[b.category]}</TableCell>
                  <TableCell>{formatPages(b.pricePages)}</TableCell>
                  <TableCell>
                    {b.previewSampleName ? (
                      <Badge
                        variant="outline"
                        className="border-emerald-300 text-[10px] text-emerald-700"
                      >
                        ✓ {b.previewSamplePageCount ?? "—"}p
                      </Badge>
                    ) : (
                      <Badge
                        variant="outline"
                        className="border-destructive/40 text-[10px] text-destructive"
                      >
                        미첨부
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(b.updatedAt)}
                  </TableCell>
                  <TableCell className="pr-6 text-right">
                    {statusBadge(b.status)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">상태 라벨 가이드</CardTitle>
          <CardDescription>
            검수 큐 흐름과 판매 상태를 한 줄로 정리.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-2 text-sm md:grid-cols-2">
          <div>
            <Badge variant="secondary">초안</Badge> 임시 저장 상태. 마법사 임시 저장.
          </div>
          <div>
            <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
              접수
            </Badge>{" "}
            검수 신청 완료, 큐 대기.
          </div>
          <div>
            <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
              검토 중
            </Badge>{" "}
            관리자가 검수 진행 중.
          </div>
          <div>
            <Badge variant="destructive">반려</Badge> 사유 확인 후 수정·재신청.
          </div>
          <div>
            <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
              판매 중
            </Badge>{" "}
            사용자 앱에 노출 중.
          </div>
          <div>
            <Badge variant="outline" className="text-muted-foreground">
              판매 중지
            </Badge>{" "}
            기존 구매자는 보유·사용 가능.
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
