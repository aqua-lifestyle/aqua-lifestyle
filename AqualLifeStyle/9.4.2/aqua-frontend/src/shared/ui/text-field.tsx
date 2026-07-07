import type { InputHTMLAttributes } from "react";

type TextFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "className"> & {
  errorMessage?: string;
  label: string;
  name: string;
};

export const TextField = ({
  errorMessage,
  id,
  label,
  name,
  ...props
}: TextFieldProps) => {
  const inputId = id ?? name;
  const errorId = `${inputId}-error`;

  return (
    <div className="flex flex-col gap-2">
      <label className="text-sm font-medium text-zinc-800" htmlFor={inputId}>
        {label}
      </label>
      <input
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className="rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100"
        id={inputId}
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
