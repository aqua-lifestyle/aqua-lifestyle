import type { InputHTMLAttributes } from "react";

import { cn } from "@/src/shared/lib/utils";

type TextFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "className"> & {
  className?: string;
  errorMessage?: string;
  label: string;
  name: string;
};

export const TextField = ({
  className,
  errorMessage,
  id,
  label,
  name,
  ...props
}: TextFieldProps) => {
  const inputId = id ?? name;
  const errorId = `${inputId}-error`;

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label
        className="text-sm font-medium text-foreground"
        htmlFor={inputId}
      >
        {label}
      </label>
      <input
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className={cn(
          "rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground outline-none transition",
          "placeholder:text-muted-foreground",
          "focus:border-accent focus:ring-2 focus:ring-accent/20",
          errorMessage && "border-error focus:border-error focus:ring-error/20",
        )}
        id={inputId}
        name={name}
        {...props}
      />
      {errorMessage ? (
        <p className="text-sm text-error" id={errorId}>
          {errorMessage}
        </p>
      ) : null}
    </div>
  );
};
