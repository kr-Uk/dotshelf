import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  globalIgnores([
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
    // MSW가 자동 생성하는 service worker — 우리 lint 룰의 적용 대상이 아님.
    "public/mockServiceWorker.js",
  ]),
]);

export default eslintConfig;
