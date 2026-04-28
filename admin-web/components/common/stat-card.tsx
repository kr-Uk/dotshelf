import type { ReactNode } from "react";

import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export function StatCard({
  label,
  value,
  caption,
  trend,
  className,
}: {
  label: string;
  value: ReactNode;
  caption?: ReactNode;
  trend?: ReactNode;
  className?: string;
}) {
  return (
    <Card className={cn("gap-2", className)} size="sm">
      <CardHeader>
        <CardDescription className="text-xs">{label}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        <div className="text-2xl font-semibold tracking-tight">{value}</div>
        {caption ? (
          <div className="text-xs text-muted-foreground">{caption}</div>
        ) : null}
        {trend}
      </CardContent>
    </Card>
  );
}
