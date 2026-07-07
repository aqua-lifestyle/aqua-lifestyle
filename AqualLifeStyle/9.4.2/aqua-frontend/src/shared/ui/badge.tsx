import type { ReactNode } from "react";

type BadgeTone = "success" | "neutral";

type BadgeProps = {
  children: ReactNode;
  tone?: BadgeTone;
};

const toneClassNames: Record<BadgeTone, string> = {
  neutral: "bg-zinc-100 text-zinc-700",
  success: "bg-emerald-50 text-emerald-700",
};

export const Badge = ({ children, tone = "neutral" }: BadgeProps) => {
  return (
    <span
      className={`rounded-full px-3 py-1 text-sm font-medium ${toneClassNames[tone]}`}
    >
      {children}
    </span>
  );
};
