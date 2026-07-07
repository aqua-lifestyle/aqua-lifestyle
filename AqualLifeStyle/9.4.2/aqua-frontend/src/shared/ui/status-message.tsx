import type { ReactNode } from "react";

type StatusMessageTone = "error" | "neutral";

type StatusMessageProps = {
  children: ReactNode;
  tone?: StatusMessageTone;
};

const toneClassNames: Record<StatusMessageTone, string> = {
  error: "border-red-200 bg-red-50 text-red-800",
  neutral: "border-zinc-300 bg-white text-zinc-600",
};

export const StatusMessage = ({
  children,
  tone = "neutral",
}: StatusMessageProps) => {
  return (
    <section
      className={`rounded-lg border border-dashed p-8 ${toneClassNames[tone]}`}
    >
      {children}
    </section>
  );
};
