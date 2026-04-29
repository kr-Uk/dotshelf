"use client";

import { useEffect } from "react";
import { AlertOctagon, RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";

/**
 * (app) 그룹 내부의 segment 에러 바운더리.
 * 사이드바·탑바는 살아있는 상태에서 본문만 fallback으로 떨어진다.
 * 컴포넌트 단위에서 throw가 나면 이 화면이 먼저 잡고, 사용자는 「다시 시도」로 회복할 수 있다.
 */
export default function AppSegmentError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // 운영에선 Sentry 등으로 보내야 함. v1.0 mock — 콘솔만.
    console.error("[admin-web] segment error:", error);
  }, [error]);

  return (
    <div className="flex h-full min-h-[60vh] items-center justify-center">
      <div className="max-w-md space-y-4 rounded-xl border border-destructive/30 bg-destructive/5 p-6 text-sm">
        <div className="flex items-center gap-2 text-destructive">
          <AlertOctagon className="size-5" />
          <h2 className="text-base font-semibold">화면을 그리는 중 오류가 발생했어요</h2>
        </div>
        <p className="text-muted-foreground">
          이 영역만 일시적으로 멈췄을 가능성이 높아요. 「다시 시도」로 복구하거나, 좌측
          메뉴에서 다른 화면으로 이동하세요. 반복되면 운영 채널에 신고 부탁드립니다.
        </p>
        {process.env.NODE_ENV !== "production" && (
          <pre className="overflow-x-auto rounded-md bg-muted/50 p-2 text-xs text-muted-foreground">
            {error.message}
            {error.digest ? `\n\ndigest: ${error.digest}` : ""}
          </pre>
        )}
        <div className="flex justify-end gap-2">
          <Button size="sm" variant="ghost" onClick={() => window.location.assign("/dashboard")}>
            홈으로
          </Button>
          <Button size="sm" onClick={reset}>
            <RefreshCw className="size-3.5" /> 다시 시도
          </Button>
        </div>
      </div>
    </div>
  );
}
