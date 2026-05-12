"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ChevronLeft } from "lucide-react";

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
import { PageHeader } from "@/components/common/page-header";
import { api, ApiError } from "@/lib/api";
import {
  PlacementToggle,
  ThemeSelector,
  serializeCompatibleTargets,
  type Placement,
} from "@/lib/asset-form";
import type {
  AdminAssetCategory,
  AdminAssetCreateInput,
  AdminAssetDetail,
} from "@/lib/types";

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
// 카테고리는 categoriesQuery 로딩 후 첫 항목을 default 로 쓴다.
const DEFAULT_ASSET_TYPE = "ASSET";
const DEFAULT_TIER = "NORMAL";
const DEFAULT_ASSET_SCOPE = "SPACE";

export default function NewAssetPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const [form, setForm] = useState<FormState>(INITIAL_FORM);

  const categoriesQuery = useQuery({
    queryKey: ["admin-asset-categories"],
    queryFn: () =>
      api<{ categories: AdminAssetCategory[] }>("/assets/categories").then(
        (response) => response.categories,
      ),
  });

  const createMutation = useMutation({
    mutationFn: (payload: AdminAssetCreateInput) =>
      api<AdminAssetDetail>("/assets", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ["admin-assets"] });
      toast.success(`${created.name} 메타데이터 생성 완료`);
      router.push(`/assets/${created.id}`);
    },
    onError: (error) => {
      const message =
        error instanceof ApiError ? error.message : "에셋 생성에 실패했습니다.";
      toast.error(message);
    },
  });

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

  const defaultCategoryCode = categoriesQuery.data?.[0]?.code ?? "";
  const canSubmit =
    form.theme.length > 0 &&
    form.subCode.length > 0 &&
    !form.subCode.startsWith("_") &&
    !form.subCode.endsWith("_") &&
    form.placement.length > 0 &&
    defaultCategoryCode.length > 0 &&
    !createMutation.isPending;

  const handleSubmit = () => {
    if (!canSubmit) return;
    const parsedPrice = Number(form.pricePages);
    const fullCode = `${form.theme}_${form.subCode}`;
    const payload: AdminAssetCreateInput = {
      code: fullCode,
      // 한국어 표시 이름을 별도로 받지 않는다 — 운영자가 영문 식별자만으로 충분.
      // Unity 카탈로그에 사용자 친화적인 이름이 필요해지면 그때 한국어 입력 칸 부활.
      name: form.subCode,
      categoryCode: defaultCategoryCode,
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
    createMutation.mutate(payload);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="신규 에셋 등록"
        description="메타데이터를 먼저 생성한 뒤, 상세 화면에서 본체와 프리뷰를 업로드하고 공개합니다."
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
              />
            </Field>
            <Field label="공급자">
              <Input
                placeholder="DotShelf Studio (선택)"
                value={form.supplier}
                onChange={(e) => updateField("supplier", e.target.value)}
              />
            </Field>
          </div>
        </CardContent>
      </Card>

      <div className="flex items-center justify-end gap-2">
        <Button
          variant="ghost"
          render={<Link href="/assets" />}
          disabled={createMutation.isPending}
        >
          취소
        </Button>
        <Button onClick={handleSubmit} disabled={!canSubmit}>
          {createMutation.isPending ? "생성 중…" : "메타데이터 생성"}
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
