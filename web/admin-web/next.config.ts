import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Docker 멀티스테이지에서 .next/standalone 만 복사하면 되도록.
  output: "standalone",
  async rewrites() {
    const adminApiBase =
      process.env.ADMIN_API_REWRITE_BASE_URL ?? "http://localhost:8080/v1/admin";
    return [
      {
        source: "/api/admin/:path*",
        destination: `${adminApiBase}/:path*`,
      },
    ];
  },
};

export default nextConfig;
