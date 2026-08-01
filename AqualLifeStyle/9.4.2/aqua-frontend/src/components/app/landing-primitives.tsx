import type { ComponentProps, ReactNode } from "react";
import Link from "next/link";

import { cn } from "@/src/shared/lib/utils";

export const landingContainerClassName =
  "mx-auto w-full max-w-7xl px-5 sm:px-8 lg:px-10";

type LandingButtonTone = "primary" | "secondary-dark" | "secondary-light" | "warm";

const buttonToneClassNames: Record<LandingButtonTone, string> = {
  primary:
    "border-aqua-violet bg-aqua-violet text-white hover:border-aqua-violet-dark hover:bg-aqua-violet-dark",
  "secondary-dark":
    "border-white/20 bg-transparent text-white hover:border-white/35 hover:bg-white/10",
  "secondary-light":
    "border-aqua-line bg-transparent text-aqua-ink hover:border-aqua-violet/35 hover:bg-white",
  warm:
    "border-aqua-cream bg-aqua-cream text-aqua-navy hover:border-white hover:bg-white",
};

type LandingLinkButtonProps = ComponentProps<typeof Link> & {
  tone?: LandingButtonTone;
};

export const LandingLinkButton = ({
  children,
  className,
  tone = "primary",
  ...props
}: LandingLinkButtonProps) => (
  <Link
    className={cn(
      "inline-flex min-h-12 items-center justify-center gap-2 rounded-aqua-control border px-6 text-sm font-semibold transition-[background-color,border-color,color,box-shadow,transform] duration-200 hover:shadow-sm motion-safe:hover:-translate-y-0.5 motion-safe:active:translate-y-0 motion-reduce:transform-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-aqua-violet",
      buttonToneClassNames[tone],
      className,
    )}
    {...props}
  >
    {children}
  </Link>
);

export const LandingEyebrow = ({
  children,
  light = false,
}: {
  children: ReactNode;
  light?: boolean;
}) => (
  <p
    className={cn(
      "text-xs font-bold uppercase leading-4 tracking-[0.16em]",
      light ? "text-aqua-lavender-strong" : "text-aqua-violet-dark",
    )}
  >
    {children}
  </p>
);
