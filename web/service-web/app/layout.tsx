import type { Metadata } from "next";
import { DM_Sans, JetBrains_Mono, Spectral } from "next/font/google";
import "./globals.css";

import { GlobalProviders } from "@/providers/app-providers";

const spectral = Spectral({
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700"],
  style: ["normal", "italic"],
  variable: "--font-spectral",
  display: "swap",
});

const dmSans = DM_Sans({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-dm-sans",
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin"],
  weight: ["400", "500"],
  variable: "--font-jetbrains-mono",
  display: "swap",
});

export const metadata: Metadata = {
  title: "DotShelf — 데스크탑에서 짓는 가상의 책장",
  description:
    "DotShelf는 macOS와 Windows 데스크탑 앱에서 좋아하는 책을 가상의 3D 책장에 모으고 꾸미는 서비스입니다.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="ko"
      className={`h-full antialiased ${spectral.variable} ${dmSans.variable} ${jetbrainsMono.variable}`}
    >
      <body className="min-h-full flex flex-col bg-background text-foreground">
        <GlobalProviders>{children}</GlobalProviders>
      </body>
    </html>
  );
}
