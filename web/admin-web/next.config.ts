import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Docker 멀티스테이지에서 .next/standalone 만 복사하면 되도록.
  output: "standalone",
};

export default nextConfig;
