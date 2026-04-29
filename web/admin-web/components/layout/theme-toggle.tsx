"use client";

import { useTheme } from "next-themes";
import { Monitor, Moon, Sun } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useMounted } from "@/lib/use-mounted";

export function ThemeToggle() {
  const { theme, setTheme, resolvedTheme } = useTheme();
  const mounted = useMounted();

  // 첫 paint 때는 system 추정값을 모르기 때문에 placeholder 아이콘만.
  const Icon = !mounted
    ? Monitor
    : resolvedTheme === "dark"
      ? Moon
      : Sun;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label="테마 전환">
            <Icon className="size-4" />
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-36">
        <DropdownMenuRadioGroup
          value={(mounted ? theme : "system") ?? "system"}
          onValueChange={(v) => setTheme(v ?? "system")}
        >
          <DropdownMenuRadioItem value="light">
            <Sun className="size-4" /> 라이트
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <Moon className="size-4" /> 다크
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="system">
            <Monitor className="size-4" /> 시스템
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
