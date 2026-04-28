"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  BookPlus,
  Megaphone,
  PackagePlus,
  TrendingUp,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { api } from "@/lib/api";
import { formatDate, formatPages, formatWon } from "@/lib/format";
import {
  REVIEW_STATUS_LABEL,
  type DashboardSummary,
  type NotificationItem,
  type ReviewItem,
} from "@/lib/types";

export default function DashboardPage() {
  const summary = useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: () => api<DashboardSummary>("/dashboard/summary"),
  });
  const reviews = useQuery({
    queryKey: ["reviews"],
    queryFn: () => api<ReviewItem[]>("/reviews"),
  });
  const notifications = useQuery({
    queryKey: ["notifications"],
    queryFn: () => api<NotificationItem[]>("/notifications"),
  });

  return (
    <div className="space-y-6">
      <div className="flex items-end justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">홈</h1>
          <p className="text-sm text-muted-foreground">
            오늘의 매출·검수·정산을 한눈에 확인하세요.
          </p>
        </div>
      </div>

      {/* 4분할 카드 */}
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Link href="/stats">
          <Card className="h-full transition hover:border-foreground/20">
            <CardHeader>
              <CardDescription>이번 달 매출</CardDescription>
              <CardTitle className="text-2xl">
                {summary.data ? (
                  formatWon(summary.data.monthlyRevenueWon)
                ) : (
                  <Skeleton className="h-7 w-32" />
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="flex items-center justify-between text-xs text-muted-foreground">
              {summary.data ? (
                <>
                  <span>{formatPages(summary.data.monthlyRevenuePages)}</span>
                  <span>{summary.data.monthlySales.toLocaleString()}건</span>
                </>
              ) : (
                <Skeleton className="h-4 w-full" />
              )}
            </CardContent>
          </Card>
        </Link>

        <Link href="/books">
          <Card className="h-full transition hover:border-foreground/20">
            <CardHeader>
              <CardDescription>진행 중인 검수</CardDescription>
              <CardTitle className="text-2xl">
                {summary.data ? (
                  `${summary.data.pendingReviews}건`
                ) : (
                  <Skeleton className="h-7 w-20" />
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground">
              SLA: 영업일 1.5일 이내 회신 평균
            </CardContent>
          </Card>
        </Link>

        <Link href="/settlements">
          <Card className="h-full transition hover:border-foreground/20">
            <CardHeader>
              <CardDescription>정산 예정</CardDescription>
              <CardTitle className="text-2xl">
                {summary.data ? (
                  formatWon(summary.data.upcomingPayoutWon)
                ) : (
                  <Skeleton className="h-7 w-32" />
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground">
              {summary.data
                ? `${formatDate(summary.data.nextPayoutDate)} (화) 입금 예정`
                : "—"}
            </CardContent>
          </Card>
        </Link>

        <Link href="/ads">
          <Card className="h-full transition hover:border-foreground/20">
            <CardHeader>
              <CardDescription>활성 광고 캠페인</CardDescription>
              <CardTitle className="text-2xl">
                {summary.data ? (
                  `${summary.data.activeCampaigns}개`
                ) : (
                  <Skeleton className="h-7 w-20" />
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground">
              주간 SoV 입찰 · 가드레일 25~30%
            </CardContent>
          </Card>
        </Link>
      </div>

      {/* 빠른 진입 */}
      <div className="grid gap-3 md:grid-cols-3">
        <QuickAction
          href="/books/new"
          icon={<BookPlus className="size-4" />}
          title="책 등록"
          desc="ISBN 매칭 → 권리 증빙 → 3D 표지 → 판매 정보"
        />
        <QuickAction
          href="/assets/new"
          icon={<PackagePlus className="size-4" />}
          title="에셋 등록"
          desc="카테고리·호환 타겟·텍스처 업로드"
        />
        <QuickAction
          href="/ads/new"
          icon={<Megaphone className="size-4" />}
          title="광고 만들기"
          desc="섹션(카테고리×연령) × 주간 SoV 입찰"
        />
      </div>

      {/* 최근 검수 + 알림 */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <div>
              <CardTitle className="text-base">최근 검수 진행 상황</CardTitle>
              <CardDescription>
                책·에셋·광고의 검수 큐 상태입니다.
              </CardDescription>
            </div>
            <Button variant="ghost" size="sm" render={<Link href="/books" />}>
              전체 보기 <ArrowRight className="size-3.5" />
            </Button>
          </CardHeader>
          <CardContent className="space-y-2">
            {!reviews.data && (
              <>
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
              </>
            )}
            {reviews.data?.map((r) => (
              <div
                key={r.id}
                className="flex items-center justify-between rounded-md border border-border bg-card px-3 py-2.5"
              >
                <div className="min-w-0">
                  <div className="flex items-center gap-2 text-sm">
                    <Badge variant="secondary" className="text-[10px]">
                      {r.type === "book" && "책"}
                      {r.type === "asset" && "에셋"}
                      {r.type === "ad" && "광고"}
                    </Badge>
                    <span className="truncate font-medium">{r.title}</span>
                  </div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {formatDate(r.submittedAt)} 접수
                  </div>
                </div>
                <Badge
                  variant={
                    r.status === "approved"
                      ? "default"
                      : r.status === "rejected"
                        ? "destructive"
                        : "outline"
                  }
                >
                  {REVIEW_STATUS_LABEL[r.status]}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <div>
              <CardTitle className="text-base">최근 알림</CardTitle>
              <CardDescription>승인·정산·광고 가드레일 등.</CardDescription>
            </div>
            <Button
              variant="ghost"
              size="sm"
              render={<Link href="/notifications" />}
            >
              전체 보기 <ArrowRight className="size-3.5" />
            </Button>
          </CardHeader>
          <CardContent className="space-y-2">
            {!notifications.data && (
              <>
                <Skeleton className="h-14 w-full" />
                <Skeleton className="h-14 w-full" />
              </>
            )}
            {notifications.data?.map((n) => (
              <div
                key={n.id}
                className="rounded-md border border-border bg-card px-3 py-2.5"
              >
                <div className="flex items-center gap-2 text-sm">
                  {!n.read && (
                    <span className="size-1.5 shrink-0 rounded-full bg-primary" />
                  )}
                  <span className="truncate font-medium">{n.title}</span>
                </div>
                <p className="mt-0.5 line-clamp-1 text-xs text-muted-foreground">
                  {n.body}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <TrendingUp className="size-4" /> 통계
          </CardTitle>
          <CardDescription>
            일·주·월 단위 매출/노출/전환은 [통계] 화면에서 자세히 확인할 수 있어요.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" render={<Link href="/stats" />}>
            통계 화면 열기 <ArrowRight className="size-4" />
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

function QuickAction({
  href,
  icon,
  title,
  desc,
}: {
  href: string;
  icon: React.ReactNode;
  title: string;
  desc: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-start gap-3 rounded-lg border border-border bg-card p-4 transition hover:border-foreground/20"
    >
      <span className="mt-0.5 inline-flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
        {icon}
      </span>
      <div>
        <div className="text-sm font-semibold">{title}</div>
        <div className="mt-0.5 text-xs text-muted-foreground">{desc}</div>
      </div>
    </Link>
  );
}
