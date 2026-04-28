const koFormatter = new Intl.NumberFormat("ko-KR");

export function formatWon(won: number): string {
  return `${koFormatter.format(Math.round(won))}원`;
}

export function formatPages(pages: number): string {
  return `${koFormatter.format(pages)}p`;
}

export function formatNumber(value: number): string {
  return koFormatter.format(value);
}

export function formatDate(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}.${String(d.getMonth() + 1).padStart(
    2,
    "0",
  )}.${String(d.getDate()).padStart(2, "0")}`;
}

export function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return `${formatDate(iso)} ${String(d.getHours()).padStart(2, "0")}:${String(
    d.getMinutes(),
  ).padStart(2, "0")}`;
}

const RTF = new Intl.RelativeTimeFormat("ko", { numeric: "auto" });

export function formatRelative(iso: string, base: Date = new Date()): string {
  const d = new Date(iso);
  const diffMs = d.getTime() - base.getTime();
  const diffMin = Math.round(diffMs / 60_000);
  if (Math.abs(diffMin) < 60) return RTF.format(diffMin, "minute");
  const diffHr = Math.round(diffMin / 60);
  if (Math.abs(diffHr) < 24) return RTF.format(diffHr, "hour");
  const diffDay = Math.round(diffHr / 24);
  return RTF.format(diffDay, "day");
}

export function maskBusinessNumber(raw: string): string {
  const digits = raw.replace(/\D/g, "");
  if (digits.length !== 10) return raw;
  return `${digits.slice(0, 3)}-${digits.slice(3, 5)}-${digits.slice(5)}`;
}

export function maskEmail(email: string): string {
  const [local, domain] = email.split("@");
  if (!local || !domain) return email;
  const head = local.slice(0, Math.max(1, local.length - 3));
  return `${head}${"*".repeat(local.length - head.length)}@${domain}`;
}
