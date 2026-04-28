"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  ImagePlus,
  PackagePlus,
  Upload,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { formatPages, pagesToWon } from "@/lib/format";
import {
  ASSET_CATEGORY_LABEL,
  ASSET_THEME_REQUIRED_SLOTS,
  COMPAT_TARGETS,
  type Asset,
  type AssetCategory,
  type ExternalLicense,
} from "@/lib/types";

const STEPS = [
  { id: 1, title: "카테고리·호환" },
  { id: 2, title: "3D / 텍스처" },
  { id: 3, title: "메타" },
  { id: 4, title: "저작권" },
  { id: 5, title: "검수 신청" },
] as const;

interface AssetDraft {
  category: AssetCategory | null;
  compatTargetIds: string[];
  themeSlots: Partial<Record<AssetCategory, string>>;
  modelFileName: string | null;
  texturesFileName: string | null;
  meta: {
    name: string;
    nameEn: string;
    description: string;
    pricePages: number;
  };
  previewFront: string | null;
  previewSide: string | null;
  /** 회전 GIF / 짧은 데모 영상. 청약철회 면책 강화. */
  motionPreview: string | null;
  licenses: ExternalLicense[];
  selfDeclaration: boolean;
}

const emptyDraft: AssetDraft = {
  category: null,
  compatTargetIds: [],
  themeSlots: {},
  modelFileName: null,
  texturesFileName: null,
  meta: { name: "", nameEn: "", description: "", pricePages: 300 },
  previewFront: null,
  previewSide: null,
  motionPreview: null,
  licenses: [],
  selfDeclaration: false,
};

export default function NewAssetPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [draft, setDraft] = useState<AssetDraft>(emptyDraft);

  const goNext = () => setStep((s) => Math.min(STEPS.length, s + 1));
  const goPrev = () => setStep((s) => Math.max(1, s - 1));

  return (
    <div className="space-y-6">
      <div>
        <Link
          href="/assets"
          className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-3" /> 에셋 목록으로
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">
          에셋 신규 등록
        </h1>
        <p className="text-sm text-muted-foreground">
          단계별로 진행됩니다. 전체 테마는 다중 슬롯 의존 — 4개 카테고리 모두
          첨부 필요.
        </p>
      </div>

      <Stepper step={step} />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div>
          {step === 1 && (
            <CategoryStep
              draft={draft}
              onNext={(d) => {
                setDraft(d);
                goNext();
              }}
            />
          )}
          {step === 2 && (
            <UploadStep
              draft={draft}
              onPrev={goPrev}
              onNext={(d) => {
                setDraft(d);
                goNext();
              }}
            />
          )}
          {step === 3 && (
            <MetaStep
              draft={draft}
              onPrev={goPrev}
              onNext={(d) => {
                setDraft(d);
                goNext();
              }}
            />
          )}
          {step === 4 && (
            <RightsStep
              draft={draft}
              onPrev={goPrev}
              onNext={(d) => {
                setDraft(d);
                goNext();
              }}
            />
          )}
          {step === 5 && (
            <ReviewStep
              draft={draft}
              onPrev={goPrev}
              onSubmitted={(asset) => {
                toast.success("검수 신청이 접수됐어요.");
                router.push(`/assets/${asset.id}`);
              }}
            />
          )}
        </div>

        <DraftSidebar draft={draft} step={step} />
      </div>
    </div>
  );
}

function Stepper({ step }: { step: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
      {STEPS.map((s, i) => {
        const active = step === s.id;
        const done = step > s.id;
        return (
          <li key={s.id} className="flex items-center gap-2">
            <span
              className={cn(
                "inline-flex size-5 items-center justify-center rounded-full border text-[10px] font-medium",
                done
                  ? "border-primary bg-primary text-primary-foreground"
                  : active
                    ? "border-primary text-primary"
                    : "border-border text-muted-foreground",
              )}
            >
              {done ? <CheckCircle2 className="size-3" /> : s.id}
            </span>
            <span
              className={cn(
                active
                  ? "text-foreground font-medium"
                  : "text-muted-foreground",
              )}
            >
              {s.title}
            </span>
            {i < STEPS.length - 1 && (
              <span className="text-muted-foreground/50">›</span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

function DraftSidebar({
  draft,
  step,
}: {
  draft: AssetDraft;
  step: number;
}) {
  return (
    <Card className="h-fit">
      <CardHeader>
        <CardTitle className="text-sm">진행 요약</CardTitle>
        <CardDescription className="text-xs">
          단계 {step}/{STEPS.length}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 text-xs">
        <Row label="카테고리">
          {draft.category ? ASSET_CATEGORY_LABEL[draft.category] : "—"}
        </Row>
        <Row label="호환 타겟">
          {draft.category === "full_theme"
            ? `슬롯 ${Object.keys(draft.themeSlots).length}/4`
            : draft.compatTargetIds.length > 0
              ? `${draft.compatTargetIds.length}개`
              : "—"}
        </Row>
        <Row label="3D 모델">
          {draft.modelFileName ?? "—"}
        </Row>
        <Row label="이름">{draft.meta.name || "—"}</Row>
        <Row label="가격">
          {draft.meta.pricePages
            ? `${formatPages(draft.meta.pricePages)} (${pagesToWon(draft.meta.pricePages).toLocaleString()}원)`
            : "—"}
        </Row>
      </CardContent>
    </Card>
  );
}

function Row({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-words text-right font-medium">
        {children}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 1: Category + compat target / theme slots
// ---------------------------------------------------------------------------

function CategoryStep({
  draft,
  onNext,
}: {
  draft: AssetDraft;
  onNext: (d: AssetDraft) => void;
}) {
  const [category, setCategory] = useState<AssetCategory | null>(
    draft.category,
  );
  const [compatTargetIds, setCompatTargetIds] = useState<string[]>(
    draft.compatTargetIds,
  );
  const [themeSlots, setThemeSlots] = useState<
    Partial<Record<AssetCategory, string>>
  >(draft.themeSlots);

  const isTheme = category === "full_theme";
  const targets = category ? COMPAT_TARGETS[category] ?? [] : [];

  const ready =
    !!category &&
    (isTheme
      ? ASSET_THEME_REQUIRED_SLOTS.every((s) => !!themeSlots[s])
      : compatTargetIds.length > 0 || targets.length === 0);

  function toggleTarget(id: string) {
    setCompatTargetIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id],
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">카테고리 + 호환 타겟</CardTitle>
        <CardDescription>
          12개 카테고리 중 선택. 전체 테마는 책장·대시보드·벽지·바닥 4개 슬롯에
          각각 에셋 ID를 첨부해야 합니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-2">
          <Label>카테고리</Label>
          <Select
            value={category ?? undefined}
            onValueChange={(v) => {
              setCategory(v as AssetCategory);
              setCompatTargetIds([]);
              setThemeSlots({});
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="카테고리 선택">
                {(v: AssetCategory) =>
                  v ? ASSET_CATEGORY_LABEL[v] : "카테고리 선택"
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {Object.entries(ASSET_CATEGORY_LABEL).map(([v, l]) => (
                <SelectItem key={v} value={v}>
                  {l}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {!!category && !isTheme && targets.length > 0 && (
          <div className="grid gap-2">
            <Label>호환 타겟 (복수 선택)</Label>
            <div className="grid gap-2">
              {targets.map((t) => (
                <label
                  key={t.id}
                  className="flex cursor-pointer items-center gap-3 rounded-md border border-border bg-card px-3 py-2 hover:bg-muted/40"
                >
                  <Checkbox
                    checked={compatTargetIds.includes(t.id)}
                    onCheckedChange={() => toggleTarget(t.id)}
                  />
                  <span className="text-sm">{t.label}</span>
                  <span className="ml-auto text-xs text-muted-foreground">
                    {t.id}
                  </span>
                </label>
              ))}
            </div>
          </div>
        )}

        {isTheme && (
          <div className="space-y-3">
            <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50/50 p-3 text-xs text-amber-900">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
              <div>
                <strong>전체 테마는 다중 슬롯 의존</strong> — 책장 / 대시보드 /
                벽지 / 바닥 4개 카테고리에 각각 의존 에셋 ID를 첨부해야 검수에
                통과합니다.
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              {ASSET_THEME_REQUIRED_SLOTS.map((s) => (
                <div key={s} className="grid gap-1.5">
                  <Label className="text-xs">
                    {ASSET_CATEGORY_LABEL[s]} 의존 에셋 ID
                  </Label>
                  <Input
                    placeholder={`예: as_th_${s}_…`}
                    value={themeSlots[s] ?? ""}
                    onChange={(e) =>
                      setThemeSlots((prev) => ({
                        ...prev,
                        [s]: e.target.value,
                      }))
                    }
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="flex justify-end pt-2">
          <Button
            size="lg"
            disabled={!ready}
            onClick={() =>
              onNext({
                ...draft,
                category,
                compatTargetIds,
                themeSlots,
              })
            }
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 2: 3D / textures upload
// ---------------------------------------------------------------------------

function UploadStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: AssetDraft;
  onPrev: () => void;
  onNext: (d: AssetDraft) => void;
}) {
  const [model, setModel] = useState<string | null>(draft.modelFileName);
  const [tex, setTex] = useState<string | null>(draft.texturesFileName);

  const ready = !!model;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">3D 모델 / 텍스처 업로드</CardTitle>
        <CardDescription>
          USDZ 또는 glTF(.glb/.gltf) + 텍스처 ZIP. 업로드 후 서버 측 폴리곤·
          사이즈·물리 단위 검증이 진행됩니다.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <FilePicker
          icon={<Upload className="size-4" />}
          label="3D 모델"
          hint="USDZ / GLB / GLTF. Mock — 실제 업로드는 v1.1에서."
          accept=".usdz,.glb,.gltf"
          fileName={model}
          onPick={(name) => setModel(name)}
        />
        <FilePicker
          icon={<Upload className="size-4" />}
          label="텍스처 (선택)"
          hint="ZIP 패키지. 4096px 권장."
          accept=".zip"
          fileName={tex}
          onPick={(name) => setTex(name)}
        />

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={!ready}
            onClick={() =>
              onNext({
                ...draft,
                modelFileName: model,
                texturesFileName: tex,
              })
            }
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function FilePicker({
  icon,
  label,
  hint,
  accept,
  fileName,
  onPick,
}: {
  icon: React.ReactNode;
  label: string;
  hint: string;
  accept: string;
  fileName: string | null;
  onPick: (name: string) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-lg border border-dashed border-border p-4 hover:bg-muted/30">
      <span className="inline-flex size-9 items-center justify-center rounded-md bg-muted">
        {icon}
      </span>
      <div className="flex-1 text-sm">
        <div className="font-medium">{fileName ?? label}</div>
        <div className="text-xs text-muted-foreground">{hint}</div>
      </div>
      <span className="rounded-md border border-border px-2 py-1 text-xs">
        파일 선택
      </span>
      <input
        type="file"
        className="hidden"
        accept={accept}
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) onPick(f.name);
        }}
      />
    </label>
  );
}

// ---------------------------------------------------------------------------
// Step 3: Meta + previews
// ---------------------------------------------------------------------------

const metaSchema = z.object({
  name: z.string().min(1, "이름(한국어)을 입력해주세요."),
  nameEn: z.string().optional(),
  description: z.string().min(1, "설명을 입력해주세요."),
  pricePages: z
    .number()
    .int("정수로 입력")
    .min(50, "최소 50p (= 500원)부터 가능합니다."),
});

function MetaStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: AssetDraft;
  onPrev: () => void;
  onNext: (d: AssetDraft) => void;
}) {
  const form = useForm<z.infer<typeof metaSchema>>({
    resolver: zodResolver(metaSchema),
    defaultValues: {
      name: draft.meta.name,
      nameEn: draft.meta.nameEn,
      description: draft.meta.description,
      pricePages: draft.meta.pricePages,
    },
  });

  const [front, setFront] = useState<string | null>(draft.previewFront);
  const [side, setSide] = useState<string | null>(draft.previewSide);
  const [motion, setMotion] = useState<string | null>(draft.motionPreview);

  // useWatch는 React Compiler 호환 — 일반 watch()는 incompatible-library 경고를 띄움.
  const pricePages = useWatch({ control: form.control, name: "pricePages" });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">메타 + 미리보기</CardTitle>
        <CardDescription>
          이름은 다국어로, 미리보기는 정면·측면 두 장이 필수입니다.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((m) => {
              if (!front || !side) {
                toast.error("정면·측면 미리보기를 모두 첨부해주세요.");
                return;
              }
              onNext({
                ...draft,
                meta: {
                  name: m.name,
                  nameEn: m.nameEn ?? "",
                  description: m.description,
                  pricePages: m.pricePages,
                },
                previewFront: front,
                previewSide: side,
                motionPreview: motion,
              });
            })}
            noValidate
          >
            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>이름 (한국어)</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="nameEn"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>이름 (영어, 선택)</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>설명</FormLabel>
                  <FormControl>
                    <Textarea rows={4} {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="pricePages"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>가격 (페이지)</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={50}
                      step={50}
                      value={Number.isFinite(field.value) ? field.value : ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        field.onChange(v === "" ? 0 : Number(v));
                      }}
                      onBlur={field.onBlur}
                      name={field.name}
                      ref={field.ref}
                    />
                  </FormControl>
                  <FormDescription>
                    {pricePages
                      ? `약 ${pagesToWon(Number(pricePages)).toLocaleString()}원`
                      : "1p = 10원"}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="space-y-2 rounded-md border border-amber-200 bg-amber-50/40 p-3 text-xs leading-relaxed text-amber-900">
              <strong>정면·측면 미리보기는 필수</strong>예요. 청약철회 면책의
              3요건 중 하나(시험 제공)를 충족하려면 사용자에게 에셋의 외형을
              두 각도 이상에서 미리 보여줘야 합니다. 회전 GIF·짧은 영상 첨부도
              권장돼요(검수 단계에서 안내).
            </div>
            <div className="grid grid-cols-2 gap-3">
              <PreviewPicker
                label="정면 미리보기 (필수)"
                fileName={front}
                onPick={setFront}
              />
              <PreviewPicker
                label="측면 미리보기 (필수)"
                fileName={side}
                onPick={setSide}
              />
            </div>

            <label className="flex cursor-pointer items-start gap-3 rounded-md border border-dashed border-emerald-300 bg-emerald-50/40 p-3 text-xs hover:bg-emerald-50">
              <span className="inline-flex size-8 items-center justify-center rounded-md bg-emerald-100">
                <ImagePlus className="size-4 text-emerald-700" />
              </span>
              <div className="flex-1">
                <div className="text-sm font-medium">
                  {motion ?? "회전 GIF / 짧은 영상 (선택, 권장)"}
                </div>
                <div className="text-muted-foreground">
                  GIF·MP4·WebM. 정지 이미지만으로 청약철회 면책이 약하다고 본 경우
                  검수에서 추가 요청될 수 있어요.
                </div>
              </div>
              <span className="rounded-md border border-border bg-background px-2 py-1">
                파일 선택
              </span>
              <input
                type="file"
                className="hidden"
                accept="image/gif,video/mp4,video/webm"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) setMotion(f.name);
                }}
              />
            </label>

            <div className="flex gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="flex-1"
                onClick={onPrev}
              >
                <ArrowLeft className="size-4" /> 이전
              </Button>
              <Button type="submit" size="lg" className="flex-1">
                다음 <ArrowRight className="size-4" />
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

function PreviewPicker({
  label,
  fileName,
  onPick,
}: {
  label: string;
  fileName: string | null;
  onPick: (name: string) => void;
}) {
  return (
    <label className="flex cursor-pointer flex-col items-start gap-2 rounded-lg border border-dashed border-border p-3 hover:bg-muted/30">
      <span className="inline-flex size-8 items-center justify-center rounded-md bg-muted">
        <ImagePlus className="size-4" />
      </span>
      <div className="text-sm font-medium">{fileName ?? label}</div>
      <div className="text-xs text-muted-foreground">PNG, 1024px 이상 권장</div>
      <input
        type="file"
        className="hidden"
        accept="image/png,image/jpeg"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) onPick(f.name);
        }}
      />
    </label>
  );
}

// ---------------------------------------------------------------------------
// Step 4: License declaration
// ---------------------------------------------------------------------------

function RightsStep({
  draft,
  onPrev,
  onNext,
}: {
  draft: AssetDraft;
  onPrev: () => void;
  onNext: (d: AssetDraft) => void;
}) {
  const [self, setSelf] = useState(draft.selfDeclaration);
  const [ext, setExt] = useState<ExternalLicense[]>(draft.licenses);

  const addExternal = () =>
    setExt((p) => [...p, { source: "", type: "cc0" }]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">저작권 확인</CardTitle>
        <CardDescription>
          자체 제작 서약 또는 외부 에셋 라이선스 명시. 검수 시 증빙 요청이 올 수
          있어요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <label className="flex cursor-pointer items-start gap-3 rounded-md border border-border bg-card px-3 py-3 hover:bg-muted/40">
          <Checkbox
            checked={self}
            onCheckedChange={(v) => setSelf(!!v)}
          />
          <div className="flex-1 text-sm">
            <div className="font-medium">자체 제작 서약</div>
            <div className="text-xs text-muted-foreground">
              본 에셋의 모든 모델·텍스처는 본인이 제작했거나 사용 권한을 보유하고
              있습니다.
            </div>
          </div>
        </label>

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label>외부 에셋 라이선스 (있는 경우)</Label>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={addExternal}
            >
              + 라이선스 추가
            </Button>
          </div>
          {ext.length === 0 && (
            <div className="rounded-md border border-dashed border-border p-3 text-xs text-muted-foreground">
              외부 참조가 없다면 비워두셔도 됩니다.
            </div>
          )}
          {ext.map((l, i) => (
            <div
              key={i}
              className="grid gap-2 rounded-md border border-border bg-card p-3 md:grid-cols-[1fr_180px_60px]"
            >
              <Input
                placeholder="출처/이름 (예: Polyhaven, 작가명)"
                value={l.source}
                onChange={(e) =>
                  setExt((prev) =>
                    prev.map((x, idx) =>
                      idx === i ? { ...x, source: e.target.value } : x,
                    ),
                  )
                }
              />
              <Select
                value={l.type}
                onValueChange={(v) =>
                  setExt((prev) =>
                    prev.map((x, idx) =>
                      idx === i
                        ? { ...x, type: v as ExternalLicense["type"] }
                        : x,
                    ),
                  )
                }
              >
                <SelectTrigger>
                  <SelectValue>
                    {(v: ExternalLicense["type"]) =>
                      v === "cc0" ? "CC0" : v === "paid" ? "유료 라이선스" : "자체"
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="cc0">CC0</SelectItem>
                  <SelectItem value="paid">유료 라이선스</SelectItem>
                  <SelectItem value="self">자체</SelectItem>
                </SelectContent>
              </Select>
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  setExt((prev) => prev.filter((_, idx) => idx !== i))
                }
              >
                삭제
              </Button>
            </div>
          ))}
        </div>

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            disabled={!self}
            onClick={() =>
              onNext({
                ...draft,
                selfDeclaration: self,
                licenses:
                  ext.length > 0
                    ? ext
                    : [{ source: "자체 제작", type: "self" }],
              })
            }
          >
            다음 <ArrowRight className="size-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Step 5: Review submission
// ---------------------------------------------------------------------------

function ReviewStep({
  draft,
  onPrev,
  onSubmitted,
}: {
  draft: AssetDraft;
  onPrev: () => void;
  onSubmitted: (asset: Asset) => void;
}) {
  const [submitting, setSubmitting] = useState(false);

  async function submit() {
    if (!draft.category) {
      toast.error("카테고리를 선택해주세요.");
      return;
    }
    setSubmitting(true);
    try {
      const created = await api<Asset>("/assets", {
        method: "POST",
        body: JSON.stringify({
          name: draft.meta.name,
          nameEn: draft.meta.nameEn || null,
          category: draft.category,
          compatTargetIds: draft.compatTargetIds,
          themeSlots:
            draft.category === "full_theme" ? draft.themeSlots : null,
          description: draft.meta.description,
          pricePages: draft.meta.pricePages,
          modelFileName: draft.modelFileName,
          texturesFileName: draft.texturesFileName,
          previewFrontUrl: draft.previewFront,
          previewSideUrl: draft.previewSide,
          motionPreviewName: draft.motionPreview,
          licenses: draft.licenses,
          status: "draft",
        }),
      });
      const submitted = await api<Asset>(`/assets/${created.id}/submit`, {
        method: "POST",
      });
      onSubmitted(submitted);
    } catch {
      toast.error("검수 신청 중 오류");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">최종 확인 후 검수 신청</CardTitle>
        <CardDescription>
          신청 후 관리자 큐에 진입합니다. 검수 결과는 인앱 + 이메일로 안내돼요.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <ReviewRow label="카테고리">
          {draft.category ? (
            <Badge>{ASSET_CATEGORY_LABEL[draft.category]}</Badge>
          ) : (
            "—"
          )}
        </ReviewRow>
        <ReviewRow label="호환 타겟">
          {draft.category === "full_theme"
            ? Object.entries(draft.themeSlots).map(([k, v]) => (
                <div key={k} className="text-xs">
                  <span className="text-muted-foreground">
                    {ASSET_CATEGORY_LABEL[k as AssetCategory]}:
                  </span>{" "}
                  {v}
                </div>
              ))
            : draft.compatTargetIds.length > 0
              ? draft.compatTargetIds.join(", ")
              : "—"}
        </ReviewRow>
        <ReviewRow label="3D 모델">{draft.modelFileName ?? "—"}</ReviewRow>
        <ReviewRow label="이름">
          <strong>{draft.meta.name}</strong>{" "}
          {draft.meta.nameEn && (
            <span className="text-muted-foreground">
              · {draft.meta.nameEn}
            </span>
          )}
        </ReviewRow>
        <ReviewRow label="설명">
          <span className="line-clamp-2 text-muted-foreground">
            {draft.meta.description}
          </span>
        </ReviewRow>
        <ReviewRow label="가격">
          {formatPages(draft.meta.pricePages)} (
          {pagesToWon(draft.meta.pricePages).toLocaleString()}원)
        </ReviewRow>
        <ReviewRow label="라이선스">
          {draft.licenses.map((l, i) => (
            <div key={i} className="text-xs">
              {l.source}{" "}
              <span className="text-muted-foreground">({l.type})</span>
            </div>
          ))}
        </ReviewRow>

        <div className="flex gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            size="lg"
            className="flex-1"
            onClick={onPrev}
            disabled={submitting}
          >
            <ArrowLeft className="size-4" /> 이전
          </Button>
          <Button
            size="lg"
            className="flex-1"
            onClick={submit}
            disabled={submitting}
          >
            {submitting ? (
              "신청 중…"
            ) : (
              <>
                <PackagePlus className="size-4" /> 검수 신청
              </>
            )}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function ReviewRow({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid grid-cols-[120px_1fr] gap-3 border-b border-border/60 pb-2 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}
