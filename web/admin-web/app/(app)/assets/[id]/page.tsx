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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PageHeader } from "@/components/common/page-header";
import { RoleGate } from "@/components/common/role-gate";
import { StatusBadge } from "@/components/common/status-badge";
import { api, ApiError } from "@/lib/api";
import { generatePreviewGlb } from "@/lib/gltf-preview";
import {
  ADMIN_ASSET_BUNDLE_MAX_BYTES,
  ADMIN_ASSET_BUNDLE_TYPES,
  ADMIN_ASSET_PREVIEW_BUNDLE_MAX_BYTES,
  ADMIN_ASSET_PREVIEW_MAX_BYTES,
  ADMIN_ASSET_PREVIEW_TYPES,
} from "@/lib/types";
import type {
  AdminAssetCategory,
  AdminAssetDetail,
  AdminAssetUpdateInput,
  AdminAssetUploadUrl,
} from "@/lib/types";
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

  const categoriesQuery = useQuery({
    queryKey: ["admin-asset-categories"],
    queryFn: () =>
      api<{ categories: AdminAssetCategory[] }>("/assets/categories").then(
        (response) => response.categories,
      ),
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
            if (file.type !== "model/gltf-binary") return;
            await generateAndUploadPreviewBundle(asset.id, file);
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

      <MetadataEditor
        key={asset.id}
        asset={asset}
        categories={categoriesQuery.data ?? []}
      />

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

function MetadataEditor({
  asset,
  categories,
}: {
  asset: AdminAssetDetail;
  categories: AdminAssetCategory[];
}) {
  const qc = useQueryClient();
  // 부모에서 key={asset.id} 로 감싸므로 asset 이 바뀌면 컴포넌트가 새로 마운트된다 —
  // 초기값을 그대로 쓰고 useEffect로 동기화하지 않는다 (cascading render 회피).
  const [name, setName] = useState(asset.name);
  const [categoryCode, setCategoryCode] = useState("");
  const [collectionCode, setCollectionCode] = useState(asset.collectionCode ?? "");
  const [assetType, setAssetType] = useState(asset.assetType);

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
    const payload: AdminAssetUpdateInput = {
      name: name.trim(),
      assetType: assetType.trim().toUpperCase(),
      collectionCode: collectionCode.trim() || null,
    };
    if (categoryCode) {
      payload.categoryCode = categoryCode;
    }
    updateMutation.mutate(payload);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>메타데이터</CardTitle>
        <CardDescription>
          이름, 타입, 컬렉션, 카테고리만 수정합니다. code는 S3 키 prefix라 고정입니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="이름">
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </Field>
          <Field label="에셋 타입">
            <Input
              value={assetType}
              onChange={(e) => setAssetType(e.target.value)}
            />
          </Field>
          <Field label="컬렉션 코드">
            <Input
              value={collectionCode}
              onChange={(e) => setCollectionCode(e.target.value)}
            />
          </Field>
          <Field label="카테고리 (변경하려면 선택)">
            <Select
              value={categoryCode || undefined}
              onValueChange={(value) => setCategoryCode(value ?? "")}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder="유지" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((category) => (
                  <SelectItem key={category.code} value={category.code}>
                    {category.name} · {category.code}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
        </div>
        <div className="mt-3 flex justify-end">
          <Button
            onClick={submit}
            disabled={updateMutation.isPending || !name.trim()}
          >
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
    if (!allowedTypes.includes(file.type)) {
      toast.error(`허용되지 않은 형식입니다: ${file.type || "unknown"}`);
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
      const uploadPath =
        kind === "bundle"
          ? `/assets/${assetId}/bundle/upload-url`
          : `/assets/${assetId}/preview-image/upload-url`;
      const presigned = await api<AdminAssetUploadUrl>(uploadPath, {
        method: "POST",
        body: JSON.stringify({
          contentType: file.type,
          byteLength: file.size,
        }),
      });

      await putToS3(presigned.uploadUrl, file, setProgress);

      const confirmPath =
        kind === "bundle"
          ? `/assets/${assetId}/bundle`
          : `/assets/${assetId}/preview-image`;
      await api<AdminAssetDetail>(confirmPath, {
        method: "PATCH",
        body: JSON.stringify({ key: presigned.key }),
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

function putToS3(
  url: string,
  file: File,
  onProgress: (percent: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", url);
    xhr.setRequestHeader("Content-Type", file.type);
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        const percent = Math.round((event.loaded / event.total) * 100);
        onProgress(percent);
      }
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        onProgress(100);
        resolve();
      } else {
        reject(new Error(`S3 PUT 실패 (HTTP ${xhr.status})`));
      }
    };
    xhr.onerror = () => reject(new Error("S3 PUT 네트워크 오류"));
    xhr.send(file);
  });
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

/**
 * 본체 .glb 를 wasm 으로 저폴리화 → preview-bundle 엔드포인트 두 번(URL 발급 / confirm)
 * 호출해서 S3 에 같이 올린다. UploadCard.onPostConfirm 의 후처리로 호출된다.
 * 결과 사이즈가 BE 한도를 넘으면 BE 가 400 으로 거른다 — 그래도 본체 publish 는 가능.
 */
async function generateAndUploadPreviewBundle(
  assetId: number,
  bodyFile: File,
): Promise<void> {
  const bytes = new Uint8Array(await bodyFile.arrayBuffer());
  const previewBytes = await generatePreviewGlb(bytes);

  if (previewBytes.byteLength > ADMIN_ASSET_PREVIEW_BUNDLE_MAX_BYTES) {
    throw new Error(
      `프리뷰 생성 결과가 너무 큽니다 (${formatBytes(previewBytes.byteLength)}). 본체에서 폴리곤 수를 더 줄여야 합니다.`,
    );
  }

  // TS lib 가 Uint8Array<ArrayBufferLike> 를 BlobPart 로 직접 못 받아서 ArrayBuffer 슬라이스 경유.
  const previewBuffer = previewBytes.buffer.slice(
    previewBytes.byteOffset,
    previewBytes.byteOffset + previewBytes.byteLength,
  ) as ArrayBuffer;
  const previewFile = new File([previewBuffer], "preview.glb", {
    type: "model/gltf-binary",
  });

  const presigned = await api<AdminAssetUploadUrl>(
    `/assets/${assetId}/preview-bundle/upload-url`,
    {
      method: "POST",
      body: JSON.stringify({
        contentType: previewFile.type,
        byteLength: previewFile.size,
      }),
    },
  );
  await putToS3(presigned.uploadUrl, previewFile, () => {});
  await api<AdminAssetDetail>(`/assets/${assetId}/preview-bundle`, {
    method: "PATCH",
    body: JSON.stringify({ key: presigned.key }),
  });
  toast.success(
    `프리뷰 자동 생성 완료 (${formatBytes(previewBytes.byteLength)})`,
  );
}
