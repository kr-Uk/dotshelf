"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Box,
  Clock,
  ImagePlus,
  PauseCircle,
  PlayCircle,
  RotateCcw,
} from "lucide-react";
import { toast } from "sonner";

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
import { Skeleton } from "@/components/ui/skeleton";
import { ResubmitDialog } from "@/components/common/resubmit-dialog";
import { api, ApiError } from "@/lib/api";
import { useNow } from "@/lib/use-now";
import { formatDate, formatPages, pagesToWon } from "@/lib/format";
import {
  ASSET_CATEGORY_LABEL,
  BOOK_STATUS_LABEL,
  COMPAT_TARGETS,
  type Asset,
  type AssetCategory,
  type AssetSaleStatus,
} from "@/lib/types";

export default function AssetDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id as string;
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ["assets", id],
    queryFn: () => api<Asset>(`/assets/${id}`),
    enabled: !!id,
  });

  const toggleSale = useMutation({
    mutationFn: () =>
      api<Asset>(`/assets/${id}/toggle-sale`, { method: "POST" }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["assets", id], updated);
      queryClient.invalidateQueries({ queryKey: ["assets"] });
      toast.success(
        updated.status === "on_sale"
          ? "판매를 재개했어요."
          : "판매를 중지했어요.",
      );
    },
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="space-y-4">
        <Link
          href="/creator/assets"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 에셋 목록으로
        </Link>
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            에셋을 찾지 못했어요.
          </CardContent>
        </Card>
      </div>
    );
  }

  const isOnSale = data.status === "on_sale";
  const canToggleSale = data.status === "on_sale" || data.status === "off_sale";

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/creator/assets"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 에셋 목록으로
        </Link>
        <div className="mt-2 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {data.name}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              <Badge variant="secondary" className="mr-2">
                {ASSET_CATEGORY_LABEL[data.category]}
              </Badge>
              {data.nameEn && <span>{data.nameEn}</span>}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={data.status} />
            {canToggleSale && (
              <Button
                variant={isOnSale ? "destructive" : "default"}
                onClick={() => toggleSale.mutate()}
                disabled={toggleSale.isPending}
              >
                {isOnSale ? (
                  <>
                    <PauseCircle className="size-4" /> 판매 중지
                  </>
                ) : (
                  <>
                    <PlayCircle className="size-4" /> 판매 재개
                  </>
                )}
              </Button>
            )}
          </div>
        </div>
      </div>

      {data.status === "rejected" && data.rejectionReason && (
        <Card className="border-destructive/30 bg-destructive/5">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base text-destructive">
              <RotateCcw className="size-4" /> 반려됐어요
            </CardTitle>
            <CardDescription className="text-destructive/80">
              사유 확인 후 수정·재신청해주세요.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="rounded-md bg-background/60 p-3 text-sm">
              {data.rejectionReason}
            </p>
            <div className="mt-3">
              <ResubmitDialog<Asset>
                rejectionReason={data.rejectionReason}
                guideTips={[
                  "정면·측면 미리보기 이미지 1024px 이상 PNG로 첨부",
                  "회전 GIF / 짧은 영상 첨부 (선택, 청약철회 면책 강화)",
                  "라이선스 증빙(외부 텍스처·폰트 사용 시) 최신인지",
                  "전체 테마라면 4개 슬롯 의존 에셋 ID 모두 매핑됐는지",
                ]}
                inlineEditor={({ onChange }) => (
                  <div className="grid grid-cols-2 gap-3">
                    <ResubmitFilePicker
                      label="정면 미리보기 재첨부"
                      onPick={(name) =>
                        onChange({ newPreviewFront: name })
                      }
                    />
                    <ResubmitFilePicker
                      label="측면 미리보기 재첨부"
                      onPick={(name) =>
                        onChange({ newPreviewSide: name })
                      }
                    />
                  </div>
                )}
                resubmit={async ({ changeNote, extra }) =>
                  api<Asset>(`/assets/${data.id}/resubmit`, {
                    method: "POST",
                    body: JSON.stringify({
                      changeNote,
                      ...extra,
                    }),
                  })
                }
                onSuccess={(updated) => {
                  queryClient.setQueryData(["assets", id], updated);
                  queryClient.invalidateQueries({ queryKey: ["assets"] });
                }}
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">기본 정보</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3 text-sm">
            <Row label="설명">{data.description || "—"}</Row>
            <Row label="3D 모델">{data.modelFileName ?? "—"}</Row>
            <Row label="텍스처">{data.texturesFileName ?? "—"}</Row>
            <Row label="모션 프리뷰">
              {data.motionPreviewName ? (
                <div className="flex items-center gap-2">
                  <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
                    첨부됨
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {data.motionPreviewName}
                  </span>
                </div>
              ) : (
                <span className="text-xs text-muted-foreground">
                  미첨부 (선택, 회전 GIF / 짧은 영상 권장)
                </span>
              )}
            </Row>
            <Row label="라이선스">
              <div className="space-y-1">
                {data.licenses.map((l, i) => (
                  <div key={i} className="text-xs">
                    {l.source}{" "}
                    <span className="text-muted-foreground">({l.type})</span>
                  </div>
                ))}
              </div>
            </Row>
            <Row label="업데이트">{formatDate(data.updatedAt)}</Row>
          </CardContent>
        </Card>

        <PriceCard asset={data} />
      </div>

      {data.category === "full_theme" && data.themeSlots && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">전체 테마 — 의존 슬롯</CardTitle>
            <CardDescription>
              4개 카테고리에 각각 의존 에셋 ID가 매핑됩니다.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2 md:grid-cols-2">
            {Object.entries(data.themeSlots).map(([k, v]) => (
              <div
                key={k}
                className="flex items-center justify-between rounded-md border border-border bg-card px-3 py-2 text-sm"
              >
                <span className="text-muted-foreground">
                  {ASSET_CATEGORY_LABEL[k as AssetCategory]}
                </span>
                <span className="font-mono text-xs">{v}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {data.category !== "full_theme" && data.compatTargetIds.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">호환 타겟</CardTitle>
            <CardDescription>
              이 에셋이 적용 가능한 본체 모델.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {data.compatTargetIds.map((id) => {
              const t = (COMPAT_TARGETS[data.category] ?? []).find(
                (x) => x.id === id,
              );
              return (
                <Badge key={id} variant="outline">
                  {t?.label ?? id}
                </Badge>
              );
            })}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Box className="size-4" /> 미리보기 시뮬레이터
          </CardTitle>
          <CardDescription>
            업로드한 에셋이 사용자 공간에 들어갔을 때를 가볍게 확인할 수 있는
            영역. 뷰어 자체는 v1.1에서 도입.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex aspect-video items-center justify-center rounded-md border border-dashed border-border bg-muted/30 text-sm text-muted-foreground">
            모델·텍스처 검수가 끝나면 이 영역에 사용자 공간 미리보기가 노출돼요.
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function StatusBadge({ status }: { status: AssetSaleStatus }) {
  if (status === "on_sale") {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100/80">
        {BOOK_STATUS_LABEL.on_sale}
      </Badge>
    );
  }
  if (status === "rejected") {
    return <Badge variant="destructive">{BOOK_STATUS_LABEL.rejected}</Badge>;
  }
  if (status === "in_review" || status === "submitted") {
    return (
      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100/80">
        {BOOK_STATUS_LABEL[status]}
      </Badge>
    );
  }
  if (status === "off_sale") {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        {BOOK_STATUS_LABEL.off_sale}
      </Badge>
    );
  }
  return <Badge variant="secondary">{BOOK_STATUS_LABEL[status]}</Badge>;
}

function Row({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid grid-cols-[120px_1fr] items-start gap-3 border-b border-border/60 pb-3 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}

function ResubmitFilePicker({
  label,
  onPick,
}: {
  label: string;
  onPick: (name: string) => void;
}) {
  const [name, setName] = useState<string | null>(null);
  return (
    <label className="flex cursor-pointer flex-col gap-1.5 rounded-md border border-dashed border-border bg-background p-3 text-xs hover:bg-muted/30">
      <span className="inline-flex size-7 items-center justify-center rounded-md bg-muted">
        <ImagePlus className="size-3.5" />
      </span>
      <span className="font-medium">{name ?? label}</span>
      <span className="text-muted-foreground">PNG, 1024px 이상</span>
      <input
        type="file"
        className="hidden"
        accept="image/png,image/jpeg"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) {
            setName(f.name);
            onPick(f.name);
          }
        }}
      />
    </label>
  );
}

function PriceCard({ asset }: { asset: Asset }) {
  const queryClient = useQueryClient();
  const [pricePages, setPricePages] = useState<number>(asset.pricePages);
  // 가격 변경 24h 쿨다운을 분 단위로 갱신.
  const now = useNow(60_000);

  const cooldownMs = asset.lastPriceChangedAt
    ? 24 * 60 * 60 * 1000 -
      (now - new Date(asset.lastPriceChangedAt).getTime())
    : 0;
  const cooldownActive = cooldownMs > 0;
  const cooldownHours = Math.ceil(cooldownMs / (60 * 60 * 1000));

  const updatePrice = useMutation({
    mutationFn: () =>
      api<Asset>(`/assets/${asset.id}/price`, {
        method: "POST",
        body: JSON.stringify({ pricePages }),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["assets", asset.id], updated);
      queryClient.invalidateQueries({ queryKey: ["assets"] });
      toast.success("가격을 업데이트했어요.");
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 409) {
        toast.error("가격은 24시간 쿨다운 후에만 변경할 수 있어요.");
      } else {
        toast.error("가격 변경 중 오류");
      }
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">가격</CardTitle>
        <CardDescription className="text-xs">
          1p = 10원. 변경 시 24시간 쿨다운.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="text-3xl font-semibold">
          {formatPages(asset.pricePages)}
        </div>
        <div className="text-xs text-muted-foreground">
          ≈ {pagesToWon(asset.pricePages).toLocaleString()}원
        </div>
        <div className="flex items-center gap-2 pt-2">
          <Input
            type="number"
            min={50}
            step={50}
            value={pricePages}
            onChange={(e) => setPricePages(Number(e.target.value))}
          />
          <Button
            size="sm"
            onClick={() => updatePrice.mutate()}
            disabled={
              cooldownActive ||
              updatePrice.isPending ||
              pricePages === asset.pricePages
            }
          >
            저장
          </Button>
        </div>
        {cooldownActive && (
          <p className="flex items-center gap-1 text-xs text-amber-700">
            <Clock className="size-3" /> 약 {cooldownHours}시간 후 변경 가능
          </p>
        )}
      </CardContent>
    </Card>
  );
}
