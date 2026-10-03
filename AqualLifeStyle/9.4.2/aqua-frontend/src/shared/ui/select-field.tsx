import { type ReactNode, type SelectHTMLAttributes, useId } from "react";

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
  const generatedId = useId();
  const selectId = id ?? `${name}-${generatedId}`;
  const errorId = `${selectId}-error`;

  return (
    <div className={cn("flex min-w-0 flex-col gap-1.5", className)}>
      <div className="text-sm font-medium text-foreground">
        <label htmlFor={selectId}>{label}</label>{props.required ? <span aria-hidden="true" className="whitespace-nowrap text-error">&nbsp;*</span> : null}
      </div>
      <select
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className={cn(
          "min-h-11 min-w-0 w-full rounded-lg border border-control-border bg-card px-3 py-2 text-sm text-foreground outline-none transition",
          "focus:border-accent focus:ring-2 focus:ring-accent/20",
          "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
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
