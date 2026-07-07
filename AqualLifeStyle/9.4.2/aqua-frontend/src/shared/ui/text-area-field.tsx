import type { TextareaHTMLAttributes } from "react";

type TextAreaFieldProps = Omit<
  TextareaHTMLAttributes<HTMLTextAreaElement>,
  "className"
> & {
  errorMessage?: string;
  label: string;
  name: string;
};

export const TextAreaField = ({
  errorMessage,
  id,
  label,
  name,
  ...props
}: TextAreaFieldProps) => {
  const textareaId = id ?? name;
  const errorId = `${textareaId}-error`;

  return (
    <div className="flex flex-col gap-2">
      <label className="text-sm font-medium text-zinc-800" htmlFor={textareaId}>
        {label}
      </label>
      <textarea
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className="resize-y rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100"
        id={textareaId}
        name={name}
        {...props}
      />
      {errorMessage ? (
        <p className="text-sm text-red-700" id={errorId}>
          {errorMessage}
        </p>
      ) : null}
    </div>
  );
};
