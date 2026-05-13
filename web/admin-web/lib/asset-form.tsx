"use client";

import { Button } from "@/components/ui/button";

// 신규 등록 (/assets/new) 과 수정 (/assets/[id]) 두 폼이 같은 칩/토글/JSON 직렬화 규칙을
// 공유한다. 컬렉션/카테고리/assetType 처럼 운영자가 신경 안 쓰기로 한 필드는 여기 두지 않는다.

export type Placement = "floor" | "wall" | "";

// 어드민에서 클릭으로 빠르게 붙일 수 있는 추천 테마.
// 칩 라벨은 한국어, 실제 themes 배열/S3 prefix 에는 영문 code 가 들어간다.
// 테마 code 에는 underscore 금지 — code 의 첫 _ 가 BE 의 S3 키 split 기준점이라
// 테마 자체에 _ 가 들어가면 경로가 깨진다 (예: DEEP_SEA_CHAIR_001 → DEEP/SEA_CHAIR_001).
export const THEME_OPTIONS: { code: string; label: string }[] = [
  { code: "DEEPSEA", label: "바다" },
  { code: "FANTASY", label: "판타지" },
  { code: "VINTAGE", label: "빈티지" },
  { code: "UNIVERSE", label: "우주" },
  { code: "FOREST", label: "숲" },
  { code: "SPOOKY", label: "공포" },
  { code: "DEFAULT", label: "기본" },
];

export function themeLabel(code: string): string {
  return THEME_OPTIONS.find((opt) => opt.code === code)?.label ?? code;
}

export function ThemeSelector({
  value,
  onChange,
}: {
  value: string;
  onChange: (next: string) => void;
}) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {THEME_OPTIONS.map((opt) => {
        const active = value === opt.code;
        return (
          <Button
            key={opt.code}
            type="button"
            variant={active ? "default" : "outline"}
            size="sm"
            onClick={() => onChange(active ? "" : opt.code)}
            title={opt.code}
          >
            {opt.label}
          </Button>
        );
      })}
    </div>
  );
}

export function PlacementToggle({
  value,
  onChange,
}: {
  value: Placement;
  onChange: (next: Placement) => void;
}) {
  const options: { code: Exclude<Placement, "">; label: string }[] = [
    { code: "floor", label: "바닥" },
    { code: "wall", label: "벽" },
  ];
  return (
    <div className="flex gap-2">
      {options.map((opt) => {
        const active = value === opt.code;
        return (
          <Button
            key={opt.code}
            type="button"
            variant={active ? "default" : "outline"}
            size="sm"
            onClick={() => onChange(active ? "" : opt.code)}
          >
            {opt.label}
          </Button>
        );
      })}
    </div>
  );
}
