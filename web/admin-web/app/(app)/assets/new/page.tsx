"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
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
import { api, ApiError } from "@/lib/api";
import {
  PlacementToggle,
  ThemeSelector,
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
import type { AdminAssetCreateInput, AdminAssetDetail } from "@/lib/types";

interface FormState {
  theme: string;
  subCode: string;
  placement: Placement;
  pricePages: string;
  supplier: string;
}

const INITIAL_FORM: FormState = {
  theme: "",
  subCode: "",
  placement: "",
  pricePages: "0",
  supplier: "",
};

// BE 필수 필드 중 폼에 안 보이는 것들. assetType 은 Unity 카탈로그에 표시되는
// 분류이지만 운영자가 신경 쓰지 않기로 해서 generic 값으로 고정. tier/assetScope 도 동일 처리.
// categoryCode 는 BE @NotBlank 라 어떤 값이든 보내야 하는데 운영자가 카테고리 선택을 안 해서
// V25 seed 에 항상 존재하는 'BOOKSHELF' 로 고정한다.
const DEFAULT_ASSET_TYPE = "ASSET";
const DEFAULT_TIER = "NORMAL";
const DEFAULT_ASSET_SCOPE = "SPACE";
const DEFAULT_CATEGORY_CODE = "BOOKSHELF";

type Phase =
  | { kind: "idle" }
  | { kind: "creating" }
  | { kind: "uploading-bundle"; progress: number }
  | { kind: "uploading-preview"; progress: number }
  | { kind: "generating-preview-bundle" }
  | { kind: "publishing" };

export default function NewAssetPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [bundleFile, setBundleFile] = useState<File | null>(null);
  const [previewFile, setPreviewFile] = useState<File | null>(null);
  const [phase, setPhase] = useState<Phase>({ kind: "idle" });
  // POST /assets 까지는 성공했는데 이후 업로드/publish 단계가 실패한 경우, 같은 code 로
  // 다시 저장을 누르면 BE 가 ASSET_CODE_ALREADY_EXISTS 로 막는다 (Asset.code unique).
  // 그래서 mutation 진행 중에 발생한 assetId 를 ref 로 보관해두고, 실패 시 운영자를
  // 상세 페이지로 유도해 거기서 per-file 업로드/publish 재시도하도록 한다.
  const createdIdRef = useRef<number | null>(null);

  const updateField = <K extends keyof FormState>(
    key: K,
    value: FormState[K],
  ) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  // 식별자(sub-code)는 영문 대문자/숫자/언더스코어만 허용. 입력값을 자동으로 대문자 변환하고
  // 다른 문자는 떨어내서 BE 의 S3 키 split 규칙을 항상 만족하도록 강제한다.
  const updateSubCode = (raw: string) => {
    const normalized = raw.toUpperCase().replace(/[^A-Z0-9_]/g, "");
    updateField("subCode", normalized);
  };

  const bundleError = useMemo(
    () =>
      validateFile(bundleFile, {
        allowed: ADMIN_ASSET_BUNDLE_TYPES as readonly string[],
        maxBytes: ADMIN_ASSET_BUNDLE_MAX_BYTES,
      }),
    [bundleFile],
  );
  const previewError = useMemo(
    () =>
      validateFile(previewFile, {
        allowed: ADMIN_ASSET_PREVIEW_TYPES as readonly string[],
        maxBytes: ADMIN_ASSET_PREVIEW_MAX_BYTES,
      }),
    [previewFile],
  );

  const metadataValid =
    form.theme.length > 0 &&
    form.subCode.length > 0 &&
    !form.subCode.startsWith("_") &&
    !form.subCode.endsWith("_") &&
    form.placement.length > 0;

  const busy = phase.kind !== "idle";
  const canSubmit =
    metadataValid &&
    bundleFile !== null &&
    previewFile !== null &&
    bundleError === null &&
    previewError === null &&
    !busy;

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!bundleFile || !previewFile) {
        throw new Error("본체와 프리뷰 이미지를 모두 선택해주세요.");
      }
      const parsedPrice = Number(form.pricePages);
      const fullCode = `${form.theme}_${form.subCode}`;
      const payload: AdminAssetCreateInput = {
        code: fullCode,
        // 한국어 표시 이름을 별도로 받지 않는다 — 운영자가 영문 식별자만으로 충분.
        // Unity 카탈로그에 사용자 친화적인 이름이 필요해지면 그때 한국어 입력 칸 부활.
        name: form.subCode,
        categoryCode: DEFAULT_CATEGORY_CODE,
        assetType: DEFAULT_ASSET_TYPE,
        collectionCode: null,
        tier: DEFAULT_TIER,
        assetScope: DEFAULT_ASSET_SCOPE,
        pricePages: Number.isFinite(parsedPrice) ? parsedPrice : 0,
        creator: null,
        supplier: form.supplier.trim() || null,
        compatibleTargets: serializeCompatibleTargets(form.theme, form.placement),
        allowsCustomization: true,
        active: false,
      };

      createdIdRef.current = null;
      setPhase({ kind: "creating" });
      const created = await api<AdminAssetDetail>("/assets", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      const assetId = created.id;
      createdIdRef.current = assetId;

      // 1) 본체. .glb 이면 뒤에서 저폴리 preview-bundle 도 같이 만든다.
      const bundleContentType = resolveContentType(bundleFile);
      setPhase({ kind: "uploading-bundle", progress: 0 });
      await uploadAndConfirmAsset({
        assetId,
        kind: "bundle",
        file: bundleFile,
        contentType: bundleContentType,
        onProgress: (progress) =>
          setPhase({ kind: "uploading-bundle", progress }),
      });

      // 2) 프리뷰 이미지.
      const previewContentType = resolveContentType(previewFile);
      setPhase({ kind: "uploading-preview", progress: 0 });
      await uploadAndConfirmAsset({
        assetId,
        kind: "preview-image",
        file: previewFile,
        contentType: previewContentType,
        onProgress: (progress) =>
          setPhase({ kind: "uploading-preview", progress }),
      });

      // 3) 저폴리 프리뷰 — 본체가 .glb 일 때만. 실패해도 publish 는 그대로 진행.
      if (bundleContentType === "model/gltf-binary") {
        setPhase({ kind: "generating-preview-bundle" });
        try {
          const size = await generateAndUploadPreviewBundle(assetId, bundleFile);
          toast.success(`저폴리 프리뷰 생성 완료 (${formatBytes(size)})`);
        } catch (postError) {
          toast.warning(
            postError instanceof Error
              ? `저폴리 프리뷰 생성 실패: ${postError.message}`
              : "저폴리 프리뷰 생성에 실패했습니다.",
          );
        }
      }

      // 4) 자동 공개.
      setPhase({ kind: "publishing" });
      await api<AdminAssetDetail>(`/assets/${assetId}/publish`, {
        method: "PUT",
      });

      return created;
    },
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ["admin-assets"] });
      qc.invalidateQueries({ queryKey: ["admin-asset", created.id] });
      toast.success(`${created.name} 등록 및 공개 완료`);
      router.push(`/assets/${created.id}`);
    },
    onError: (error) => {
      const message =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : "에셋 등록에 실패했습니다.";
      toast.error(message);
      // draft 가 만들어졌으면 목록 캐시 무효화 + 상세 페이지로 자동 이동.
      // 거기서 미완 업로드/publish 를 per-file UI 로 재시도할 수 있다.
      qc.invalidateQueries({ queryKey: ["admin-assets"] });
      const draftId = createdIdRef.current;
      if (draftId !== null) {
        qc.invalidateQueries({ queryKey: ["admin-asset", draftId] });
        router.push(`/assets/${draftId}`);
      }
    },
    onSettled: () => {
      setPhase({ kind: "idle" });
    },
  });

  const handleSubmit = () => {
    if (!canSubmit) return;
    saveMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="신규 에셋 등록"
        description="메타데이터와 파일을 모두 채운 뒤 저장을 누르면 한 번에 업로드되고 공개됩니다."
        actions={
          <Button variant="outline" size="sm" render={<Link href="/assets" />}>
            <ChevronLeft className="size-3.5" /> 목록으로
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>메타데이터</CardTitle>
          <CardDescription>
            테마와 식별자가 정해지면 S3 저장 경로가 자동으로 결정됩니다.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          <Field label="테마" required>
            <ThemeSelector
              value={form.theme}
              onChange={(value) => updateField("theme", value)}
            />
          </Field>

          <Field label="식별자 (영문)" required>
            <Input
              placeholder="CHAIR_001"
              value={form.subCode}
              onChange={(e) => updateSubCode(e.target.value)}
              maxLength={70}
              className="font-mono"
              disabled={busy}
            />
          </Field>

          <Field label="S3 저장 경로">
            <S3PathPreview theme={form.theme} subCode={form.subCode} />
          </Field>

          <Field label="배치 (placement)" required>
            <PlacementToggle
              value={form.placement}
              onChange={(value) => updateField("placement", value)}
            />
          </Field>

          <div className="grid gap-4 md:grid-cols-2">
            <Field label="가격 (페이지)">
              <Input
                type="number"
                min={0}
                value={form.pricePages}
                onChange={(e) => updateField("pricePages", e.target.value)}
                disabled={busy}
              />
            </Field>
            <Field label="공급자">
              <Input
                placeholder="DotShelf Studio (선택)"
                value={form.supplier}
                onChange={(e) => updateField("supplier", e.target.value)}
                disabled={busy}
              />
            </Field>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <FileStagingCard
          title="본체 (Unity AssetBundle)"
          description={`최대 ${formatBytes(ADMIN_ASSET_BUNDLE_MAX_BYTES)} · ${ADMIN_ASSET_BUNDLE_TYPES.join(", ")}`}
          icon={<Package className="size-4" />}
          accept=".bundle,.zip,.glb,application/octet-stream,application/zip,model/gltf-binary"
          file={bundleFile}
          error={bundleError}
          disabled={busy}
          onPick={setBundleFile}
        />
        <FileStagingCard
          title="프리뷰 이미지"
          description={`최대 ${formatBytes(ADMIN_ASSET_PREVIEW_MAX_BYTES)} · ${ADMIN_ASSET_PREVIEW_TYPES.join(", ")}`}
          icon={<ImageIcon className="size-4" />}
          accept="image/jpeg,image/png,image/webp"
          file={previewFile}
          error={previewError}
          disabled={busy}
          showImagePreview
          onPick={setPreviewFile}
        />
      </div>

      <PhaseIndicator phase={phase} />

      <div className="flex items-center justify-end gap-2">
        <Button
          variant="ghost"
          render={<Link href="/assets" />}
          disabled={busy}
        >
          취소
        </Button>
        <Button onClick={handleSubmit} disabled={!canSubmit}>
          {busy ? "처리 중…" : "저장"}
        </Button>
      </div>
    </div>
  );
}

function Field({
  label,
  required,
  children,
}: {
  label: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="grid gap-1.5">
      <Label className="text-xs text-muted-foreground">
        {label}
        {required ? (
          <span className="text-destructive" aria-hidden>
            {" *"}
          </span>
        ) : null}
      </Label>
      {children}
    </div>
  );
}

function S3PathPreview({
  theme,
  subCode,
}: {
  theme: string;
  subCode: string;
}) {
  if (!theme || !subCode) {
    return (
      <div className="rounded-md border border-dashed bg-muted/20 px-3 py-2 text-xs text-muted-foreground">
        테마와 식별자를 모두 입력하면 저장 경로가 표시됩니다.
      </div>
    );
  }
  return (
    <div className="rounded-md border bg-muted/30 px-3 py-2 text-xs font-mono break-all">
      assets/<span className="font-semibold text-foreground">{theme}/{subCode}</span>/bundle/...
    </div>
  );
}

// 픽한 파일을 staging 만 한다. 실제 업로드는 부모의 "저장" 흐름에서 일괄 수행.
// 검증은 부모에서 useMemo 로 미리 돌리고 error 를 prop 으로 내려준다.
function FileStagingCard({
  title,
  description,
  icon,
  accept,
  file,
  error,
  disabled,
  showImagePreview,
  onPick,
}: {
  title: string;
  description: string;
  icon: React.ReactNode;
  accept: string;
  file: File | null;
  error: string | null;
  disabled: boolean;
  showImagePreview?: boolean;
  onPick: (file: File | null) => void;
}) {
  // 이미지 미리보기용 object URL. file 이 바뀌거나 카드가 unmount 될 때 명시적 revoke.
  // 운영자가 같은 카드에서 파일을 여러 번 바꿔 고를 수 있어 누적 누수를 막는다.
  const previewUrl = useMemo(() => {
    if (!showImagePreview || !file) return null;
    return URL.createObjectURL(file);
  }, [showImagePreview, file]);
  useEffect(() => {
    if (!previewUrl) return;
    return () => URL.revokeObjectURL(previewUrl);
  }, [previewUrl]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {icon}
          {title}
        </CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <Input
          type="file"
          accept={accept}
          disabled={disabled}
          onChange={(event) => {
            const picked = event.target.files?.[0] ?? null;
            // input value 를 비워서 같은 파일을 다시 선택할 수 있게 한다.
            event.target.value = "";
            onPick(picked);
          }}
        />

        {file ? (
          <div className="space-y-2 rounded-md border bg-muted/30 p-3 text-xs">
            <div className="break-all font-mono text-[11px]">{file.name}</div>
            <div className="text-muted-foreground">
              {resolveContentType(file) || "unknown"} · {formatBytes(file.size)}
            </div>
            {previewUrl ? (
              // 운영자 본인이 검수용으로 보는 이미지라 next/image 대신 단순 <img>.
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={previewUrl}
                alt="프리뷰"
                className="max-h-40 rounded border"
              />
            ) : null}
            <Button
              variant="ghost"
              size="sm"
              disabled={disabled}
              onClick={() => onPick(null)}
            >
              제거
            </Button>
          </div>
        ) : (
          <div className="rounded-md border border-dashed bg-muted/20 p-3 text-xs text-muted-foreground">
            아직 파일을 선택하지 않았습니다.
          </div>
        )}

        {error ? (
          <div className="flex items-start gap-1 text-xs text-destructive">
            <CircleAlert className="mt-0.5 size-3.5 shrink-0" />
            <span>{error}</span>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function PhaseIndicator({ phase }: { phase: Phase }) {
  if (phase.kind === "idle") return null;

  const phaseLabel: Record<Exclude<Phase["kind"], "idle">, string> = {
    creating: "메타데이터 생성 중",
    "uploading-bundle": "본체 업로드 중",
    "uploading-preview": "프리뷰 이미지 업로드 중",
    "generating-preview-bundle": "저폴리 프리뷰 생성 중",
    publishing: "공개 처리 중",
  };
  const label = phaseLabel[phase.kind];
  const progress =
    phase.kind === "uploading-bundle" || phase.kind === "uploading-preview"
      ? phase.progress
      : null;

  return (
    <Card>
      <CardContent className="space-y-2 py-4">
        <div className="flex items-center gap-2 text-sm">
          <UploadCloud className="size-4" />
          <span>{label}…</span>
          {progress != null ? (
            <span className="ml-auto font-mono text-xs text-muted-foreground">
              {progress}%
            </span>
          ) : null}
        </div>
        {progress != null ? <Progress value={progress} /> : null}
      </CardContent>
    </Card>
  );
}

function validateFile(
  file: File | null,
  opts: { allowed: readonly string[]; maxBytes: number },
): string | null {
  if (!file) return null;
  const contentType = resolveContentType(file);
  if (!opts.allowed.includes(contentType)) {
    return `허용되지 않은 형식입니다: ${contentType || file.type || "unknown"}`;
  }
  if (file.size > opts.maxBytes) {
    return `파일이 너무 큽니다 (${formatBytes(file.size)} > ${formatBytes(opts.maxBytes)})`;
  }
  return null;
}
