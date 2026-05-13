"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  CheckCircle2,
  ChevronLeft,
  CircleAlert,
  Image as ImageIcon,
  Package,
  UploadCloud,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { api, ApiError } from "@/lib/api";
import {
  PlacementToggle,
  ThemeSelector,
  deserializeCompatibleTargets,
  serializeCompatibleTargets,
  type Placement,
} from "@/lib/asset-form";
import {
  formatBytes,
  generateAndUploadPreviewBundle,
  resolveContentType,
  uploadAndConfirmAsset,
} from "@/lib/asset-upload";
import {
  ADMIN_ASSET_BUNDLE_MAX_BYTES,
  ADMIN_ASSET_BUNDLE_TYPES,
  ADMIN_ASSET_PREVIEW_MAX_BYTES,
  ADMIN_ASSET_PREVIEW_TYPES,
} from "@/lib/types";
import type { AdminAssetDetail, AdminAssetUpdateInput } from "@/lib/types";
type UploadKind = "bundle" | "preview-image";

export default function AssetDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const assetId = Number(params.id);
  const qc = useQueryClient();

  const detailQuery = useQuery({
    queryKey: ["admin-asset", assetId],
    queryFn: () => api<AdminAssetDetail>(`/assets/${assetId}`),
    enabled: Number.isFinite(assetId),
  });

  const asset = detailQuery.data;

  if (detailQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">불러오는 중…</div>;
  }
  if (!asset) {
    return (
      <div className="space-y-2">
        <div className="text-sm text-muted-foreground">
          에셋을 찾을 수 없습니다.
        </div>
        <Button variant="outline" size="sm" render={<Link href="/assets" />}>
          <ChevronLeft className="size-3.5" /> 목록으로
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={asset.name}
        description={
          <span className="font-mono text-xs">
            #{asset.id} · {asset.code} · v{asset.version}
          </span>
        }
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge tone={asset.active ? "success" : "warn"}>
              {asset.active ? "공개" : "비공개"}
            </StatusBadge>
            <Button variant="outline" size="sm" render={<Link href="/assets" />}>
              <ChevronLeft className="size-3.5" /> 목록
            </Button>
          </div>
        }
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <UploadCard
          assetId={asset.id}
          kind="bundle"
          title="본체 (Unity AssetBundle)"
          description={`최대 ${formatBytes(ADMIN_ASSET_BUNDLE_MAX_BYTES)} · ${ADMIN_ASSET_BUNDLE_TYPES.join(", ")}`}
          allowedTypes={[...ADMIN_ASSET_BUNDLE_TYPES]}
          maxBytes={ADMIN_ASSET_BUNDLE_MAX_BYTES}
          currentUrl={asset.assetUrl}
          currentMeta={
            asset.assetUrl
              ? {
                  contentType: asset.contentType,
                  fileSize: asset.fileSize,
                  fileHash: asset.fileHash,
                  version: asset.version,
                }
              : null
          }
          icon={<Package className="size-4" />}
          accept=".bundle,.zip,.glb,application/octet-stream,application/zip,model/gltf-binary"
          onPostConfirm={async (file) => {
            // .glb 본체에 한해서 wasm gltfpack 으로 저폴리화한 preview.glb 를 만들어 같이 올린다.
            // bundle/zip 은 Unity Editor 도구 없이는 저폴리화 못 하니까 skip.
            // Windows 등에서 .glb 의 file.type 이 빈 string 으로 오는 케이스를 잡으려면
            // 확장자 fallback 까지 보는 resolveContentType 을 거쳐야 한다.
            if (resolveContentType(file) !== "model/gltf-binary") return;
            const size = await generateAndUploadPreviewBundle(asset.id, file);
            toast.success(`프리뷰 자동 생성 완료 (${formatBytes(size)})`);
          }}
          onAfter={() => {
            qc.invalidateQueries({ queryKey: ["admin-asset", asset.id] });
            qc.invalidateQueries({ queryKey: ["admin-assets"] });
          }}
        />
        <UploadCard
          assetId={asset.id}
          kind="preview-image"
          title="프리뷰 이미지"
          description={`최대 ${formatBytes(ADMIN_ASSET_PREVIEW_MAX_BYTES)} · ${ADMIN_ASSET_PREVIEW_TYPES.join(", ")}`}
          allowedTypes={[...ADMIN_ASSET_PREVIEW_TYPES]}
          maxBytes={ADMIN_ASSET_PREVIEW_MAX_BYTES}
          currentUrl={asset.previewImageUrl}
          currentMeta={null}
          icon={<ImageIcon className="size-4" />}
          accept="image/jpeg,image/png,image/webp"
          showPreview
          onAfter={() => {
            qc.invalidateQueries({ queryKey: ["admin-asset", asset.id] });
            qc.invalidateQueries({ queryKey: ["admin-assets"] });
          }}
        />
      </div>

      <MetadataEditor key={asset.id} asset={asset} />

      <Card>
        <CardHeader>
          <CardTitle>공개 상태</CardTitle>
          <CardDescription>
            본체와 프리뷰가 모두 업로드된 뒤에만 공개할 수 있습니다.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <PublishStateLine asset={asset} />
            <div className="ml-auto">
              <RoleGate action="asset.publish" fallback="disable" reason="슈퍼 관리자만">
                <PublishButton
                  assetId={asset.id}
                  disabled={!asset.assetUrl || !asset.previewImageUrl}
                  active={asset.active}
                  onDone={() => {
                    qc.invalidateQueries({ queryKey: ["admin-asset", asset.id] });
                    qc.invalidateQueries({ queryKey: ["admin-assets"] });
                    router.refresh();
                  }}
                />
              </RoleGate>
            </div>
          </div>
          {asset.previewBundleUrl ? (
            <div className="rounded-md border bg-muted/30 p-2 text-xs text-muted-foreground">
              <div>
                저폴리 프리뷰:{" "}
                <span className="break-all font-mono">{asset.previewBundleUrl}</span>
              </div>
              <div>
                {asset.previewBundleContentType ?? "—"}
                {asset.previewBundleSize != null
                  ? ` · ${formatBytes(asset.previewBundleSize)}`
                  : ""}
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}

function PublishStateLine({ asset }: { asset: AdminAssetDetail }) {
  const bundleOk = !!asset.assetUrl;
  const previewOk = !!asset.previewImageUrl;
  const previewBundleOk = !!asset.previewBundleUrl;
  return (
    <div className="flex flex-wrap items-center gap-3 text-sm">
      <StateChip ok={bundleOk} label="본체" />
      <StateChip ok={previewOk} label="프리뷰 이미지" />
      <StateChip
        ok={previewBundleOk}
        label="저폴리 프리뷰"
        optional
        hint="본체가 .glb 면 업로드 시 자동 생성됩니다. 없어도 공개 가능."
      />
      {asset.active ? (
        <span className="inline-flex items-center gap-1 text-emerald-700 dark:text-emerald-300">
          <CheckCircle2 className="size-3.5" /> 사용자 카탈로그에 노출됨
        </span>
      ) : null}
    </div>
  );
}

function StateChip({
  ok,
  label,
  optional,
  hint,
}: {
  ok: boolean;
  label: string;
  optional?: boolean;
  hint?: string;
}) {
  const missingTone = optional ? "text-muted-foreground" : "text-amber-600";
  return (
    <span className="inline-flex items-center gap-1" title={hint}>
      {ok ? (
        <CheckCircle2 className="size-3.5 text-emerald-600" />
      ) : (
        <CircleAlert className={`size-3.5 ${missingTone}`} />
      )}
      {label} {ok ? "준비 완료" : optional ? "없음 (선택)" : "미업로드"}
    </span>
  );
}

function PublishButton({
  assetId,
  disabled,
  active,
  onDone,
}: {
  assetId: number;
  disabled: boolean;
  active: boolean;
  onDone: () => void;
}) {
  const publishMutation = useMutation({
    mutationFn: () =>
      api<AdminAssetDetail>(`/assets/${assetId}/publish`, { method: "PUT" }),
    onSuccess: () => {
      toast.success("에셋이 사용자 카탈로그에 공개되었습니다.");
      onDone();
    },
    onError: (error) => {
      toast.error(
        error instanceof ApiError ? error.message : "공개 처리에 실패했습니다.",
      );
    },
  });
  return (
    <Button
      onClick={() => publishMutation.mutate()}
      disabled={disabled || publishMutation.isPending || active}
      title={
        disabled
          ? "본체와 프리뷰 이미지를 모두 업로드해야 합니다."
          : active
            ? "이미 공개된 에셋입니다."
            : undefined
      }
    >
      {active ? "공개됨" : publishMutation.isPending ? "공개 중…" : "공개"}
    </Button>
  );
}

function MetadataEditor({ asset }: { asset: AdminAssetDetail }) {
  const qc = useQueryClient();
  // 부모에서 key={asset.id} 로 감싸므로 asset 이 바뀌면 컴포넌트가 새로 마운트된다 —
  // 초기값을 그대로 쓰고 useEffect로 동기화하지 않는다 (cascading render 회피).
  const initial = deserializeCompatibleTargets(asset.compatibleTargets);
  const [theme, setTheme] = useState(initial.theme);
  const [placement, setPlacement] = useState<Placement>(initial.placement);
  const [pricePages, setPricePages] = useState(String(asset.pricePages));
  const [supplier, setSupplier] = useState(asset.supplier ?? "");

  const updateMutation = useMutation({
    mutationFn: (payload: AdminAssetUpdateInput) =>
      api<AdminAssetDetail>(`/assets/${asset.id}`, {
        method: "PATCH",
        body: JSON.stringify(payload),
      }),
    onSuccess: () => {
      toast.success("메타데이터가 수정되었습니다.");
      qc.invalidateQueries({ queryKey: ["admin-asset", asset.id] });
      qc.invalidateQueries({ queryKey: ["admin-assets"] });
    },
    onError: (error) => {
      toast.error(
        error instanceof ApiError ? error.message : "수정에 실패했습니다.",
      );
    },
  });

  const submit = () => {
    const parsedPrice = Number(pricePages);
    const payload: AdminAssetUpdateInput = {
      compatibleTargets: serializeCompatibleTargets(theme, placement),
      pricePages: Number.isFinite(parsedPrice) ? parsedPrice : 0,
      supplier: supplier.trim() || null,
    };
    updateMutation.mutate(payload);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>메타데이터</CardTitle>
        <CardDescription>
          테마, 배치, 가격, 공급자만 수정합니다. code 와 식별자는 S3 키 prefix 라 고정.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <Field label="code (고정)">
          <Input value={asset.code} disabled className="font-mono" />
        </Field>

        <Field label="테마">
          <ThemeSelector value={theme} onChange={setTheme} />
        </Field>

        <Field label="배치 (placement)">
          <PlacementToggle value={placement} onChange={setPlacement} />
        </Field>

        <div className="grid gap-4 md:grid-cols-2">
          <Field label="가격 (페이지)">
            <Input
              type="number"
              min={0}
              value={pricePages}
              onChange={(e) => setPricePages(e.target.value)}
            />
          </Field>
          <Field label="공급자">
            <Input
              placeholder="DotShelf Studio (선택)"
              value={supplier}
              onChange={(e) => setSupplier(e.target.value)}
            />
          </Field>
        </div>

        <div className="flex justify-end">
          <Button onClick={submit} disabled={updateMutation.isPending}>
            {updateMutation.isPending ? "저장 중…" : "메타데이터 저장"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

interface UploadCardProps {
  assetId: number;
  kind: UploadKind;
  title: string;
  description: string;
  allowedTypes: string[];
  maxBytes: number;
  currentUrl: string | null;
  currentMeta: {
    contentType: string | null;
    fileSize: number | null;
    fileHash: string | null;
    version: number;
  } | null;
  icon: React.ReactNode;
  accept: string;
  showPreview?: boolean;
  /** confirm 완료 후 추가 후처리 (예: 본체 .glb 의 저폴리 프리뷰 자동 생성). 실패해도 toast warning 만. */
  onPostConfirm?: (file: File) => Promise<void>;
  onAfter: () => void;
}

function UploadCard({
  assetId,
  kind,
  title,
  description,
  allowedTypes,
  maxBytes,
  currentUrl,
  currentMeta,
  icon,
  accept,
  showPreview,
  onPostConfirm,
  onAfter,
}: UploadCardProps) {
  const [progress, setProgress] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [phase, setPhase] = useState<string | null>(null);

  const uploadAndConfirm = async (file: File) => {
    // 브라우저가 .glb 같은 확장자의 MIME 을 OS 별로 모르거나 비워서 반환할 때가 있어 (특히 Windows)
    // 파일 확장자 기반 fallback 으로 보정한다. 보정 후에도 미매치면 그때 reject.
    const contentType = resolveContentType(file);
    if (!allowedTypes.includes(contentType)) {
      toast.error(
        `허용되지 않은 형식입니다: ${contentType || file.type || "unknown"}`,
      );
      return;
    }
    if (file.size > maxBytes) {
      toast.error(
        `파일이 너무 큽니다 (${formatBytes(file.size)} > ${formatBytes(maxBytes)})`,
      );
      return;
    }

    setBusy(true);
    setProgress(0);
    setPhase("업로드 중");
    try {
      await uploadAndConfirmAsset({
        assetId,
        kind,
        file,
        contentType,
        onProgress: setProgress,
      });
      toast.success(
        kind === "bundle" ? "본체 업로드 완료" : "프리뷰 업로드 완료",
      );

      if (onPostConfirm) {
        setPhase("후처리 중");
        setProgress(null);
        try {
          await onPostConfirm(file);
        } catch (postError) {
          // 후처리 실패는 본체 업로드 자체를 깨지 않음 — 본체는 이미 confirm 됐고
          // 프리뷰 번들은 publish 게이트 밖이라 나중에 재시도 가능.
          toast.warning(
            postError instanceof Error
              ? `후처리 실패: ${postError.message}`
              : "후처리에 실패했습니다.",
          );
        }
      }
      onAfter();
    } catch (error) {
      toast.error(
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "업로드에 실패했습니다.",
      );
    } finally {
      setBusy(false);
      setProgress(null);
      setPhase(null);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {icon}
          {title}
        </CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {currentUrl ? (
          <div className="space-y-1 rounded-md border bg-muted/30 p-3 text-xs">
            <div className="flex items-center gap-1 text-emerald-700 dark:text-emerald-300">
              <CheckCircle2 className="size-3.5" /> 업로드됨
            </div>
            <div className="break-all font-mono text-[11px] text-muted-foreground">
              {currentUrl}
            </div>
            {currentMeta ? (
              <div className="text-muted-foreground">
                {currentMeta.contentType ?? "—"} · v{currentMeta.version}
                {currentMeta.fileSize != null
                  ? ` · ${formatBytes(currentMeta.fileSize)}`
                  : ""}
              </div>
            ) : null}
            {showPreview ? (
              // 운영자 본인이 검수용으로 보는 이미지라 next/image 대신 단순 <img>.
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={currentUrl}
                alt="현재 프리뷰"
                className="max-h-40 rounded border"
              />
            ) : null}
          </div>
        ) : (
          <div className="rounded-md border border-dashed bg-muted/20 p-3 text-xs text-muted-foreground">
            아직 업로드된 파일이 없습니다.
          </div>
        )}

        <RoleGate action="asset.manage" fallback="disable" reason="슈퍼 관리자만">
          <Input
            type="file"
            accept={accept}
            disabled={busy}
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = "";
              if (file) void uploadAndConfirm(file);
            }}
          />
        </RoleGate>

        {progress != null ? (
          <div className="space-y-1">
            <Progress value={progress} />
            <div className="text-xs text-muted-foreground">
              <UploadCloud className="mr-1 inline size-3" />
              {phase ?? "업로드 중"} · {progress}%
            </div>
          </div>
        ) : phase ? (
          <div className="text-xs text-muted-foreground">
            <UploadCloud className="mr-1 inline size-3" />
            {phase}…
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid gap-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}
