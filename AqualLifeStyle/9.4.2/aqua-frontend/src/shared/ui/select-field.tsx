import type { ReactNode, SelectHTMLAttributes } from "react";

type SelectFieldProps = Omit<
  SelectHTMLAttributes<HTMLSelectElement>,
  "className"
> & {
  children: ReactNode;
  errorMessage?: string;
  label: string;
  name: string;
};

export const SelectField = ({
  children,
  errorMessage,
  id,
  label,
  name,
  ...props
}: SelectFieldProps) => {
  const selectId = id ?? name;
  const errorId = `${selectId}-error`;

  return (
    <div className="flex flex-col gap-2">
      <label className="text-sm font-medium text-zinc-800" htmlFor={selectId}>
        {label}
      </label>
      <select
        aria-describedby={errorMessage ? errorId : undefined}
        aria-invalid={Boolean(errorMessage)}
        className="rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base text-zinc-950 outline-none transition focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100"
        id={selectId}
        name={name}
        {...props}
      >
        {children}
      </select>
      {errorMessage ? (
        <p className="text-sm text-red-700" id={errorId}>
          {errorMessage}
        </p>
      ) : null}
    </div>
  );
};
