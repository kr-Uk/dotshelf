const wonFormatter = new Intl.NumberFormat("ko-KR");

export function formatWon(won: number): string {
  return `${wonFormatter.format(Math.round(won))}원`;
}

export function formatPages(pages: number): string {
  return `${wonFormatter.format(pages)}p`;
}

export function pagesToWon(pages: number, ratePerPageWon = 10): number {
  return pages * ratePerPageWon;
}

export function formatDate(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}.${String(d.getMonth() + 1).padStart(
    2,
    "0",
  )}.${String(d.getDate()).padStart(2, "0")}`;
}

export function maskBusinessNumber(raw: string): string {
  const digits = raw.replace(/\D/g, "");
  if (digits.length !== 10) return raw;
  return `${digits.slice(0, 3)}-${digits.slice(3, 5)}-${digits.slice(5)}`;
}
