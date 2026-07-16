import Link from "next/link";
import type { ComponentProps, ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type LinkButtonVariant = "primary" | "secondary" | "outline" | "ghost";
type LinkButtonSize = "sm" | "md" | "lg";

type LinkButtonProps = ComponentProps<typeof Link> & {
  children: ReactNode;
  size?: LinkButtonSize;
  variant?: LinkButtonVariant;
};

const variantClassNames: Record<LinkButtonVariant, string> = {
  ghost: "bg-transparent text-foreground hover:bg-muted",
  outline:
    "border border-border bg-card text-foreground hover:bg-muted hover:border-border/80",
  primary:
    "bg-accent text-white hover:bg-accent-dark shadow-sm shadow-accent/20",
  secondary: "bg-primary text-background hover:bg-primary-light",
};

const sizeClassNames: Record<LinkButtonSize, string> = {
  sm: "h-8 px-3 text-xs",
  md: "h-10 px-4 text-sm",
  lg: "h-12 px-6 text-base",
};

export const LinkButton = ({
  children,
  className,
  size = "md",
  variant = "outline",
  ...props
}: LinkButtonProps) => {
  return (
    <Link
      className={cn(
        "inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-all",
        variantClassNames[variant],
        sizeClassNames[size],
        className,
      )}
      {...props}
    >
      {children}
    </Link>
  );
};
