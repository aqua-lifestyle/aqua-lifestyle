import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
};

export const Button = ({ children, className, type, ...props }: ButtonProps) => {
  return (
    <button
      className={[
        "rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-emerald-800 disabled:cursor-not-allowed disabled:bg-zinc-300",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      type={type ?? "button"}
      {...props}
    >
      {children}
    </button>
  );
};
