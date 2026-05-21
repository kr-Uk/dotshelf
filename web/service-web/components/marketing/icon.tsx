import type { SVGProps } from "react";

type IconName =
  | "shelf"
  | "eye"
  | "cloud"
  | "download"
  | "apple"
  | "windows"
  | "arrow-right"
  | "check"
  | "plus"
  | "mail"
  | "menu"
  | "close"
  | "sparkle"
  | "book"
  | "users"
  | "external";

type IconProps = {
  name: IconName;
  size?: number;
  strokeWidth?: number;
} & Omit<SVGProps<SVGSVGElement>, "name">;

export function Icon({
  name,
  size = 20,
  strokeWidth = 1.6,
  className,
  ...rest
}: IconProps) {
  const stroke = {
    width: size,
    height: size,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    className,
    "aria-hidden": true,
    ...rest,
  };

  switch (name) {
    case "shelf":
      return (
        <svg {...stroke}>
          <rect x="3" y="4" width="18" height="14" rx="1.5" />
          <path d="M3 11h18" />
          <path d="M7 4v7M11 4v7M15 4v7M9 11v7M13 11v7M17 11v7" />
        </svg>
      );
    case "eye":
      return (
        <svg {...stroke}>
          <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
          <circle cx="12" cy="12" r="3" />
        </svg>
      );
    case "cloud":
      return (
        <svg {...stroke}>
          <path d="M7 18a4 4 0 0 1-.7-7.94A6 6 0 0 1 18 9.5 4.5 4.5 0 0 1 17 18H7Z" />
        </svg>
      );
    case "download":
      return (
        <svg {...stroke}>
          <path d="M12 4v11" />
          <path d="m7 11 5 5 5-5" />
          <path d="M5 20h14" />
        </svg>
      );
    case "apple":
      return (
        <svg
          width={size}
          height={size}
          viewBox="0 0 24 24"
          fill="currentColor"
          className={className}
          aria-hidden
          {...rest}
        >
          <path d="M16.4 12.7c0-2.5 2-3.7 2.1-3.8-1.2-1.7-3-1.9-3.7-2-1.6-.2-3 .9-3.8.9-.8 0-2-.9-3.3-.9-1.7 0-3.3 1-4.2 2.5-1.8 3.1-.5 7.7 1.3 10.2.9 1.2 1.9 2.6 3.2 2.6 1.3-.1 1.8-.8 3.4-.8 1.5 0 2 .8 3.3.8 1.4 0 2.3-1.3 3.1-2.5.7-1 1.2-1.9 1.5-2.9-1.7-.7-2.9-2.4-2.9-4.1ZM14 5.5c.7-.9 1.2-2.1 1.1-3.3-1 0-2.3.7-3 1.6-.6.7-1.2 2-1.1 3.1 1.2.1 2.3-.6 3-1.4Z" />
        </svg>
      );
    case "windows":
      return (
        <svg
          width={size}
          height={size}
          viewBox="0 0 24 24"
          fill="currentColor"
          className={className}
          aria-hidden
          {...rest}
        >
          <path d="M3 5.5 11 4.4v7.1H3V5.5Zm0 7.5h8v7.1L3 18.5V13Zm9-8.7L21.5 3v8.5H12V4.3ZM12 12.5h9.5V21L12 19.7V12.5Z" />
        </svg>
      );
    case "arrow-right":
      return (
        <svg {...stroke}>
          <path d="M5 12h14" />
          <path d="m13 6 6 6-6 6" />
        </svg>
      );
    case "check":
      return (
        <svg {...stroke}>
          <path d="m5 12 4 4 10-10" />
        </svg>
      );
    case "plus":
      return (
        <svg {...stroke}>
          <path d="M12 5v14M5 12h14" />
        </svg>
      );
    case "mail":
      return (
        <svg {...stroke}>
          <rect x="3" y="5" width="18" height="14" rx="2" />
          <path d="m3 7 9 6 9-6" />
        </svg>
      );
    case "menu":
      return (
        <svg {...stroke}>
          <path d="M4 7h16M4 12h16M4 17h16" />
        </svg>
      );
    case "close":
      return (
        <svg {...stroke}>
          <path d="M6 6l12 12M18 6 6 18" />
        </svg>
      );
    case "sparkle":
      return (
        <svg {...stroke}>
          <path d="M12 4v6M12 14v6M4 12h6M14 12h6" />
        </svg>
      );
    case "book":
      return (
        <svg {...stroke}>
          <path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V4Z" />
          <path d="M5 17a3 3 0 0 1 3-3h11" />
        </svg>
      );
    case "users":
      return (
        <svg {...stroke}>
          <circle cx="9" cy="8" r="3" />
          <path d="M3 19a6 6 0 0 1 12 0" />
          <circle cx="17" cy="9" r="2.5" />
          <path d="M15 19a5 5 0 0 1 6-4.6" />
        </svg>
      );
    case "external":
      return (
        <svg {...stroke}>
          <path d="M7 17 17 7M9 7h8v8" />
        </svg>
      );
    default:
      return null;
  }
}
