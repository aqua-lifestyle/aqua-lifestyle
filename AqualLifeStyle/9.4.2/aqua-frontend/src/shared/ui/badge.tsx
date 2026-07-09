import type { ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type BadgeTone =
  | "neutral"
  | "success"
  | "warning"
  | "error"
  | "info"
  | "accent";

type BadgeProps = {
  children: ReactNode;
  className?: string;
  tone?: BadgeTone;
};

const toneClassNames: Record<BadgeTone, string> = {
  accent: "bg-accent/10 text-accent-dark ring-accent/20",
  error: "bg-error/10 text-error ring-error/20",
  info: "bg-info/10 text-info ring-info/20",
  neutral: "bg-muted text-muted-foreground ring-border",
  success: "bg-success/10 text-success ring-success/20",
  warning: "bg-warning/10 text-warning ring-warning/20",
};

export const Badge = ({ children, className, tone = "neutral" }: BadgeProps) => {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1",
        toneClassNames[tone],
        className,
      )}
    >
      {children}
    </span>
  );
};
