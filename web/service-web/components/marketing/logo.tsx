import Link from "next/link";

export function MarketingLogo({ size = 28 }: { size?: number }) {
  return (
    <Link
      href="/"
      className="flex items-center gap-2 text-charcoal hover:opacity-80 transition"
    >
      <span
        className="inline-flex items-center justify-center"
        style={{ width: size, height: size }}
      >
        <svg width={size} height={size} viewBox="0 0 32 32" fill="none">
          <rect x="3" y="6" width="26" height="20" rx="3" fill="#A38069" />
          <path d="M3 12h26" stroke="#F6F2EC" strokeWidth="1.4" />
          <path d="M3 19h26" stroke="#F6F2EC" strokeWidth="1.4" />
          <circle cx="9" cy="9" r="1.4" fill="#F6F2EC" />
        </svg>
      </span>
      <span className="serif text-[20px] tracking-tight font-semibold">
        DotShelf
      </span>
    </Link>
  );
}
