import { type TextareaHTMLAttributes, useId } from "react";

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
  const generatedId = useId();
  const textareaId = id ?? `${name}-${generatedId}`;
  const errorId = `${textareaId}-error`;

  return (
    <div className={cn("flex min-w-0 flex-col gap-1.5", className)}>
      <div className="text-sm font-medium text-foreground">
        <label htmlFor={textareaId}>{label}</label>{props.required ? <span aria-hidden="true" className="whitespace-nowrap text-error">&nbsp;*</span> : null}
      </div>
      <textarea
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className={cn(
          "min-w-0 w-full resize-y rounded-lg border border-control-border bg-card px-3 py-2 text-sm text-foreground outline-none transition",
          "placeholder:text-muted-foreground",
          "focus:border-accent focus:ring-2 focus:ring-accent/20",
          "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
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
