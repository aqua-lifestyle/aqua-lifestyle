import type { TextareaHTMLAttributes } from "react";

import { cn } from "@/src/shared/lib/utils";

type TextAreaFieldProps = Omit<
  TextareaHTMLAttributes<HTMLTextAreaElement>,
  "className"
> & {
  className?: string;
  errorMessage?: string;
  label: string;
  name: string;
};

export const TextAreaField = ({
  className,
  errorMessage,
  id,
  label,
  name,
  ...props
}: TextAreaFieldProps) => {
  const textareaId = id ?? name;
  const errorId = `${textareaId}-error`;

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label
        className="text-sm font-medium text-foreground"
        htmlFor={textareaId}
      >
        {label}
      </label>
      <textarea
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className={cn(
          "resize-y rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground outline-none transition",
          "placeholder:text-muted-foreground",
          "focus:border-accent focus:ring-2 focus:ring-accent/20",
          errorMessage && "border-error focus:border-error focus:ring-error/20",
        )}
        id={textareaId}
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
