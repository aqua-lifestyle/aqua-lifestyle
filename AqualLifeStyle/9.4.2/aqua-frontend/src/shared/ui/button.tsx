import { Loader2 } from "lucide-react";
import type { ButtonHTMLAttributes, ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type ButtonVariant = "primary" | "secondary" | "outline" | "ghost" | "danger";
type ButtonSize = "sm" | "md" | "lg";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
  isLoading?: boolean;
  size?: ButtonSize;
  variant?: ButtonVariant;
};

const variantClassNames: Record<ButtonVariant, string> = {
  danger:
    "bg-error/10 text-error-ink hover:bg-error/20 focus-visible:outline-error",
  ghost:
    "bg-transparent text-foreground hover:bg-muted",
  outline:
    "border border-control-border bg-card text-foreground hover:bg-muted",
  primary:
    "bg-accent text-accent-foreground hover:bg-accent-dark shadow-sm shadow-accent/20",
  secondary:
    "bg-primary text-background hover:bg-primary-light",
};

const sizeClassNames: Record<ButtonSize, string> = {
  sm: "min-h-11 px-3 py-2 text-xs lg:min-h-9 lg:py-1.5",
  md: "min-h-11 px-4 py-2 text-sm",
  lg: "min-h-12 px-6 py-2.5 text-base",
};

export const Button = ({
  children,
  className,
  disabled,
  isLoading,
  size = "md",
  type = "button",
  variant = "primary",
  ...props
}: ButtonProps) => {
  return (
    <button
      className={cn(
        "inline-flex max-w-full items-center justify-center gap-2 rounded-lg text-center font-semibold leading-tight transition-all [&>svg]:shrink-0",
        "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
        "disabled:cursor-not-allowed disabled:opacity-50",
        variantClassNames[variant],
        sizeClassNames[size],
        className,
      )}
      disabled={disabled || isLoading}
      aria-busy={isLoading || undefined}
      type={type}
      {...props}
    >
      {isLoading ? (
        <Loader2 className="size-4 animate-spin" aria-hidden="true" />
      ) : null}
      {children}
    </button>
  );
};
