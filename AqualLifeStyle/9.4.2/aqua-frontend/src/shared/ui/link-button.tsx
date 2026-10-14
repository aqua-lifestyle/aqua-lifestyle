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
    "border border-control-border bg-card text-foreground hover:bg-muted",
  primary:
    "bg-accent text-accent-foreground hover:bg-accent-dark shadow-sm shadow-accent/20",
  secondary: "bg-primary text-background hover:bg-primary-light",
};

const sizeClassNames: Record<LinkButtonSize, string> = {
  sm: "min-h-11 px-3 py-2 text-xs lg:min-h-9 lg:py-1.5",
  md: "min-h-11 px-4 py-2 text-sm",
  lg: "min-h-12 px-6 py-2.5 text-base",
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
        "inline-flex max-w-full items-center justify-center gap-2 rounded-lg text-center font-semibold leading-tight transition-all [&>svg]:shrink-0",
        "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
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
