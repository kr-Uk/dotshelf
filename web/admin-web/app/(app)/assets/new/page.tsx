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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { PageHeader } from "@/components/common/page-header";
import { api, ApiError } from "@/lib/api";
import type {
  AdminAssetCategory,
  AdminAssetCreateInput,
  AdminAssetDetail,
} from "@/lib/types";

interface FormState {
  code: string;
  name: string;
  categoryCode: string;
  assetType: string;
  collectionCode: string;
  tier: string;
  assetScope: string;
  pricePages: string;
  creator: string;
  supplier: string;
  compatibleTargets: string;
  allowsCustomization: boolean;
}

const INITIAL_FORM: FormState = {
  code: "",
  name: "",
  categoryCode: "",
  assetType: "",
  collectionCode: "",
  tier: "NORMAL",
  assetScope: "SPACE",
  pricePages: "0",
  creator: "",
  supplier: "",
  compatibleTargets: "",
  allowsCustomization: true,
};

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

  const canSubmit =
    form.code.trim().length > 0 &&
    form.name.trim().length > 0 &&
    form.categoryCode.length > 0 &&
    form.assetType.trim().length > 0 &&
    !createMutation.isPending;

  const handleSubmit = () => {
    if (!canSubmit) return;
    const parsedPrice = Number(form.pricePages);
    const payload: AdminAssetCreateInput = {
      code: form.code.trim(),
      name: form.name.trim(),
      categoryCode: form.categoryCode,
      assetType: form.assetType.trim().toUpperCase(),
      collectionCode: form.collectionCode.trim() || null,
      tier: form.tier.trim() || null,
      assetScope: form.assetScope.trim() || null,
      pricePages: Number.isFinite(parsedPrice) ? parsedPrice : 0,
      creator: form.creator.trim() || null,
      supplier: form.supplier.trim() || null,
      compatibleTargets: form.compatibleTargets.trim() || null,
      allowsCustomization: form.allowsCustomization,
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
            code는 한 번 정해지면 바꿀 수 없어 S3 키 prefix가 됩니다. 영문 대문자/숫자/언더스코어 권장.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="code (식별자)" required>
              <Input
                placeholder="DEEP_SEA_CHAIR_001"
                value={form.code}
                onChange={(e) => updateField("code", e.target.value)}
                maxLength={80}
              />
            </Field>
            <Field label="이름" required>
              <Input
                placeholder="깊은바다 의자"
                value={form.name}
                onChange={(e) => updateField("name", e.target.value)}
                maxLength={100}
              />
            </Field>

            <Field label="카테고리" required>
              <Select
                value={form.categoryCode || undefined}
                onValueChange={(value) =>
                  updateField("categoryCode", value ?? "")
                }
              >
                <SelectTrigger className="w-full">
                  <SelectValue
                    placeholder={
                      categoriesQuery.isLoading
                        ? "카테고리 불러오는 중…"
                        : "카테고리 선택"
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {(categoriesQuery.data ?? []).map((category) => (
                    <SelectItem key={category.code} value={category.code}>
                      {category.name} · {category.code}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label="에셋 타입 (assetType)" required>
              <Input
                placeholder="CHAIR / WALLPAPER / THEME 등"
                value={form.assetType}
                onChange={(e) => updateField("assetType", e.target.value)}
              />
            </Field>

            <Field label="컬렉션 코드">
              <Input
                placeholder="DEEP_SEA (선택)"
                value={form.collectionCode}
                onChange={(e) => updateField("collectionCode", e.target.value)}
                maxLength={80}
              />
            </Field>
            <Field label="가격 (페이지)">
              <Input
                type="number"
                min={0}
                value={form.pricePages}
                onChange={(e) => updateField("pricePages", e.target.value)}
              />
            </Field>

            <Field label="등급 (tier)">
              <Input
                placeholder="NORMAL / RARE / EVENT"
                value={form.tier}
                onChange={(e) => updateField("tier", e.target.value)}
              />
            </Field>
            <Field label="배치 범위 (assetScope)">
              <Input
                placeholder="SPACE / PROFILE 등"
                value={form.assetScope}
                onChange={(e) => updateField("assetScope", e.target.value)}
              />
            </Field>

            <Field label="크리에이터">
              <Input
                placeholder="DotShelf (선택)"
                value={form.creator}
                onChange={(e) => updateField("creator", e.target.value)}
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

          <div className="mt-4 grid gap-3">
            <Field label="호환 정보 (compatibleTargets, JSON 문자열)">
              <Textarea
                placeholder='{"placement": ["floor"], "themes": ["DEEP_SEA"]}'
                rows={3}
                value={form.compatibleTargets}
                onChange={(e) =>
                  updateField("compatibleTargets", e.target.value)
                }
              />
            </Field>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={form.allowsCustomization}
                onChange={(e) =>
                  updateField("allowsCustomization", e.target.checked)
                }
              />
              테마 적용 후 일부 카테고리 변경 허용 (allowsCustomization)
            </label>
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
