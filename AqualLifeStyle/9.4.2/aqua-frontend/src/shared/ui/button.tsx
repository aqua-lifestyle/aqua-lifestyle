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
    "bg-error/10 text-error hover:bg-error/20 focus:ring-error/30",
  ghost:
    "bg-transparent text-foreground hover:bg-muted",
  outline:
    "border border-border bg-card text-foreground hover:bg-muted hover:border-border/80",
  primary:
    "bg-accent text-white hover:bg-accent-dark shadow-sm shadow-accent/20 focus:ring-accent/30",
  secondary:
    "bg-primary text-white hover:bg-primary-light",
};

const sizeClassNames: Record<ButtonSize, string> = {
  sm: "h-8 px-3 text-xs",
  md: "h-10 px-4 text-sm",
  lg: "h-12 px-6 text-base",
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
        "inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-all",
        "focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-background",
        "disabled:cursor-not-allowed disabled:opacity-50",
        variantClassNames[variant],
        sizeClassNames[size],
        className,
      )}
      disabled={disabled || isLoading}
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
