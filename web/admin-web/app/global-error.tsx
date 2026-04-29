"use client";

import { useEffect } from "react";

/**
 * Next.js의 최상위 에러 바운더리. root layout 자체가 throw하면 이게 잡는다.
 * (app)/error.tsx가 못 잡는 경우의 최후 fallback.
 * Note: global-error.tsx는 자체 <html>·<body>를 렌더해야 함.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[admin-web] global error:", error);
  }, [error]);

  return (
    <html lang="ko">
      <body
        style={{
          fontFamily:
            'system-ui, -apple-system, "Apple SD Gothic Neo", "Noto Sans KR", sans-serif',
          background: "#0b0b0c",
          color: "#fafafa",
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: 24,
        }}
      >
        <div
          style={{
            maxWidth: 480,
            border: "1px solid rgba(255,255,255,0.1)",
            borderRadius: 12,
            padding: 24,
          }}
        >
          <h1 style={{ fontSize: 18, fontWeight: 600, marginBottom: 12 }}>
            관리자 도구를 불러오지 못했어요
          </h1>
          <p style={{ color: "#a0a0a0", fontSize: 14, lineHeight: 1.6 }}>
            화면 자체를 띄우는 단계에서 문제가 발생했습니다. 새로고침으로 회복되지
            않으면 운영 채널에 신고 부탁드립니다.
          </p>
          {process.env.NODE_ENV !== "production" ? (
            <pre
              style={{
                marginTop: 12,
                fontSize: 12,
                color: "#888",
                whiteSpace: "pre-wrap",
                overflowX: "auto",
              }}
            >
              {error.message}
              {error.digest ? `\n\ndigest: ${error.digest}` : ""}
            </pre>
          ) : null}
          <button
            onClick={reset}
            style={{
              marginTop: 16,
              padding: "8px 14px",
              borderRadius: 8,
              border: "1px solid rgba(255,255,255,0.2)",
              background: "transparent",
              color: "#fff",
              cursor: "pointer",
              fontSize: 13,
            }}
          >
            다시 시도
          </button>
        </div>
      </body>
    </html>
  );
}
