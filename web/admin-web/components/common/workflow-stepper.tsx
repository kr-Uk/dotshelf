import { Check } from "lucide-react";

import { cn } from "@/lib/utils";

export type StepperStep = {
  key: string;
  label: string;
};

/**
 * 단계별 진행도(예: 위탁 제작의 수령 → 스캔 → 후처리 → 적용)를 한 줄로 시각화.
 * `currentKey`가 null 이면 0단계(아직 시작 전)로 표시.
 */
export function WorkflowStepper({
  steps,
  currentKey,
  className,
}: {
  steps: StepperStep[];
  currentKey: string | null | undefined;
  className?: string;
}) {
  const currentIndex = currentKey ? steps.findIndex((s) => s.key === currentKey) : -1;
  return (
    <ol className={cn("flex w-full items-center gap-1", className)}>
      {steps.map((s, i) => {
        const passed = i < currentIndex;
        const active = i === currentIndex;
        return (
          <li key={s.key} className="flex flex-1 items-center gap-1">
            <div className="flex flex-col items-center gap-1">
              <span
                className={cn(
                  "inline-flex size-6 items-center justify-center rounded-full border text-[11px] font-semibold",
                  passed
                    ? "border-emerald-600 bg-emerald-600 text-white"
                    : active
                      ? "border-primary bg-primary text-primary-foreground"
                      : "border-border bg-muted text-muted-foreground",
                )}
              >
                {passed ? <Check className="size-3.5" /> : i + 1}
              </span>
              <span
                className={cn(
                  "text-[11px] whitespace-nowrap",
                  active
                    ? "font-medium text-foreground"
                    : passed
                      ? "text-muted-foreground"
                      : "text-muted-foreground/70",
                )}
              >
                {s.label}
              </span>
            </div>
            {i < steps.length - 1 && (
              <div
                className={cn(
                  "mb-4 h-px flex-1",
                  i < currentIndex ? "bg-emerald-600" : "bg-border",
                )}
              />
            )}
          </li>
        );
      })}
    </ol>
  );
}
