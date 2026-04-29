import type { Metadata } from "next";
import "./globals.css";

import { GlobalProviders } from "@/providers/app-providers";

export const metadata: Metadata = {
  title: "Dotshelf — 책으로 짓는 작은 책장",
  description:
    "Dotshelf는 좋아하는 책을 가상의 책장에 모으고 꾸미는 서비스입니다.",
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
