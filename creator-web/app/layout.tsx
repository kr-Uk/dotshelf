import type { Metadata } from "next";
import "./globals.css";

import { GlobalProviders } from "@/providers/app-providers";

export const metadata: Metadata = {
  title: "Dotshelf 크리에이터",
  description: "Dotshelf 크리에이터 백오피스 — 책·에셋·광고를 등록하고 정산까지.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ko" className="h-full antialiased">
      <body className="min-h-full flex flex-col bg-background text-foreground">
        <GlobalProviders>{children}</GlobalProviders>
      </body>
    </html>
  );
}
