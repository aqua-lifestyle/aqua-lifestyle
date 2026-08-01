import type { ComponentProps, ReactNode } from "react";
import Link from "next/link";

import { cn } from "@/src/shared/lib/utils";

export const landingContainerClassName =
  "mx-auto w-full max-w-7xl px-5 sm:px-8 lg:px-10";

export const landingSectionClassName = "py-20 sm:py-24 lg:py-28";

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
      "inline-flex min-h-12 items-center justify-center gap-2 rounded-aqua-control border px-6 text-sm font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-aqua-violet",
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

type LandingSectionHeadingProps = {
  description: string;
  eyebrow: string;
  id: string;
  light?: boolean;
  title: string;
};

export const LandingSectionHeading = ({
  description,
  eyebrow,
  id,
  light = false,
  title,
}: LandingSectionHeadingProps) => (
  <div className="max-w-3xl">
    <LandingEyebrow light={light}>{eyebrow}</LandingEyebrow>
    <h2
      className={cn(
        "mt-4 text-3xl font-semibold leading-[1.1] tracking-[-0.035em] text-balance sm:text-4xl lg:text-5xl",
        light ? "text-white" : "text-aqua-ink",
      )}
      id={id}
    >
      {title}
    </h2>
    <p
      className={cn(
        "mt-5 max-w-2xl text-base leading-7 sm:text-lg",
        light ? "text-aqua-dark-muted" : "text-aqua-muted",
      )}
    >
      {description}
    </p>
  </div>
);

export const LandingBadge = ({ children }: { children: ReactNode }) => (
  <span className="inline-flex min-h-8 items-center gap-2 rounded-full border border-white/15 bg-white/[0.06] px-3 text-xs font-semibold text-white/85">
    {children}
  </span>
);

export const LandingCard = ({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) => (
  <article
    className={cn(
      "rounded-aqua-card border border-aqua-line bg-aqua-surface p-6 sm:p-8",
      className,
    )}
  >
    {children}
  </article>
);
