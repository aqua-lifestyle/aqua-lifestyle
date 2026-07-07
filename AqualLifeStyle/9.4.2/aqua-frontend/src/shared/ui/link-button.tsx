import Link from "next/link";
import type { ComponentProps, ReactNode } from "react";

type LinkButtonVariant = "primary" | "secondary";

type LinkButtonProps = ComponentProps<typeof Link> & {
  children: ReactNode;
  variant?: LinkButtonVariant;
};

const variantClassNames: Record<LinkButtonVariant, string> = {
  primary:
    "bg-emerald-700 text-white hover:bg-emerald-800",
  secondary:
    "border border-zinc-300 bg-white text-zinc-800 hover:bg-zinc-100",
};

export const LinkButton = ({
  children,
  className,
  variant = "secondary",
  ...props
}: LinkButtonProps) => {
  return (
    <Link
      className={[
        "rounded-lg px-4 py-2 text-center text-sm font-semibold transition",
        variantClassNames[variant],
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      {...props}
    >
      {children}
    </Link>
  );
};
