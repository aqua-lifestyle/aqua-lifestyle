import type { ReactNode, SelectHTMLAttributes } from "react";

import { cn } from "@/src/shared/lib/utils";

type SelectFieldProps = Omit<
  SelectHTMLAttributes<HTMLSelectElement>,
  "className"
> & {
  children: ReactNode;
  className?: string;
  errorMessage?: string;
  label: string;
  name: string;
};

export const SelectField = ({
  children,
  className,
  errorMessage,
  id,
  label,
  name,
  ...props
}: SelectFieldProps) => {
  const selectId = id ?? name;
  const errorId = `${selectId}-error`;

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label
        className="text-sm font-medium text-foreground"
        htmlFor={selectId}
      >
        {label}
      </label>
      <select
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className={cn(
          "rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground outline-none transition",
          "focus:border-accent focus:ring-2 focus:ring-accent/20",
          errorMessage && "border-error focus:border-error focus:ring-error/20",
        )}
        id={selectId}
        name={name}
        {...props}
      >
        {children}
      </select>
      {errorMessage ? (
        <p className="text-sm text-error" id={errorId}>
          {errorMessage}
        </p>
      ) : null}
    </div>
  );
};
