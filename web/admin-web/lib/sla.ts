import type { ReportSeverity } from "./types";

export function reportSlaDueAt(severity: ReportSeverity, createdAt: string): string {
  const offsetHours = severity === "p1" ? 1 : severity === "p2" ? 24 : 72;
  return new Date(new Date(createdAt).getTime() + offsetHours * 3_600_000).toISOString();
}
