"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Package, Plus } from "lucide-react";

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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { TableSkeletonRows } from "@/components/common/table-skeleton";
import { api } from "@/lib/api";
import type {
  AdminAssetListItem,
  AdminAssetListResponse,
} from "@/lib/types";
import { formatDateTime } from "@/lib/format";

type ActiveFilter = "all" | "true" | "false";

export default function AssetsListPage() {
  const [q, setQ] = useState("");
  const [collection, setCollection] = useState("");
  const [type, setType] = useState("");
  const [active, setActive] = useState<ActiveFilter>("all");
  const [cursor, setCursor] = useState<string | null>(null);

  const queryString = new URLSearchParams();
  if (q.trim()) queryString.set("q", q.trim());
  if (collection.trim()) queryString.set("collection", collection.trim());
  if (type.trim()) queryString.set("type", type.trim());
  if (active !== "all") queryString.set("active", active);
  queryString.set("limit", "20");
  if (cursor) queryString.set("cursor", cursor);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ["admin-assets", q, collection, type, active, cursor],
    queryFn: () =>
      api<AdminAssetListResponse>(`/assets?${queryString.toString()}`),
  });

  const items = data?.items ?? [];

  const resetCursor = () => setCursor(null);

  return (
    <div className="space-y-6">
      <PageHeader
        title="에셋 관리"
        description="Unity 본체와 카탈로그 프리뷰 이미지를 S3에 직업로드해 사용자 카탈로그에 공개합니다."
        actions={
          <RoleGate action="asset.manage" fallback="hide">
            <Button render={<Link href="/assets/new" />}>
              <Plus className="size-3.5" /> 신규 등록
            </Button>
          </RoleGate>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center gap-2">
            <CardTitle className="flex items-center gap-2">
              <Package className="size-4" /> 에셋 목록
            </CardTitle>
            <CardDescription className="ml-auto text-xs">
              총 {data?.totalCount ?? 0}건
            </CardDescription>
          </div>
          <div className="flex flex-wrap items-center gap-2 pt-2">
            <Input
              placeholder="코드 · 이름 검색"
              value={q}
              onChange={(event) => {
                setQ(event.target.value);
                resetCursor();
              }}
              className="max-w-xs"
            />
            <Input
              placeholder="컬렉션 코드"
              value={collection}
              onChange={(event) => {
                setCollection(event.target.value);
                resetCursor();
              }}
              className="max-w-44"
            />
            <Input
              placeholder="에셋 타입"
              value={type}
              onChange={(event) => {
                setType(event.target.value);
                resetCursor();
              }}
              className="max-w-40"
            />
            <Select
              value={active}
              onValueChange={(value) => {
                setActive((value ?? "all") as ActiveFilter);
                resetCursor();
              }}
            >
              <SelectTrigger size="sm" className="w-32">
                <SelectValue placeholder="공개 상태" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">상태 전체</SelectItem>
                <SelectItem value="true">공개</SelectItem>
                <SelectItem value="false">비공개</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>코드 · 이름</TableHead>
                <TableHead>타입 · 컬렉션</TableHead>
                <TableHead className="text-right">가격</TableHead>
                <TableHead className="text-center">본체</TableHead>
                <TableHead className="text-center">프리뷰</TableHead>
                <TableHead>상태</TableHead>
                <TableHead>업데이트</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading && <TableSkeletonRows rows={6} cols={7} />}
              {!isLoading && items.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="text-center text-sm text-muted-foreground"
                  >
                    조건에 맞는 에셋이 없습니다.
                  </TableCell>
                </TableRow>
              )}
              {items.map((asset) => (
                <AssetRow key={asset.id} asset={asset} />
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="flex items-center justify-end gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={!cursor}
          onClick={() => setCursor(null)}
        >
          처음으로
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={!data?.nextCursor || isFetching}
          onClick={() => setCursor(data?.nextCursor ?? null)}
        >
          다음 페이지
        </Button>
      </div>
    </div>
  );
}

function AssetRow({ asset }: { asset: AdminAssetListItem }) {
  return (
    <TableRow className="cursor-pointer" data-asset-id={asset.id}>
      <TableCell>
        <Link
          href={`/assets/${asset.id}`}
          className="block hover:underline"
        >
          <div className="font-mono text-xs text-muted-foreground">
            {asset.code}
          </div>
          <div className="font-medium">{asset.name}</div>
        </Link>
      </TableCell>
      <TableCell>
        <div className="text-sm">{asset.assetType}</div>
        <div className="text-xs text-muted-foreground">
          {asset.collectionCode ?? "—"}
        </div>
      </TableCell>
      <TableCell className="text-right tabular-nums">
        {asset.pricePages.toLocaleString()}p
      </TableCell>
      <TableCell className="text-center">
        <StatusBadge tone={asset.hasAssetUrl ? "success" : "neutral"}>
          {asset.hasAssetUrl ? `v${asset.version}` : "없음"}
        </StatusBadge>
      </TableCell>
      <TableCell className="text-center">
        <StatusBadge tone={asset.hasPreviewImage ? "success" : "neutral"}>
          {asset.hasPreviewImage ? "있음" : "없음"}
        </StatusBadge>
      </TableCell>
      <TableCell>
        <StatusBadge tone={asset.active ? "success" : "warn"}>
          {asset.active ? "공개" : "비공개"}
        </StatusBadge>
      </TableCell>
      <TableCell className="text-xs text-muted-foreground">
        {formatDateTime(asset.updatedAt)}
      </TableCell>
    </TableRow>
  );
}
