import type { LucideIcon } from "lucide-react";
import {
  ArrowRight,
  BadgeCheck,
  CircleCheck,
  Eye,
  LayoutDashboard,
  Leaf,
  ListChecks,
  Network,
  PackageSearch,
  Search,
  ShieldCheck,
  SlidersHorizontal,
  Users,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";

import { LandingAccountActions } from "./landing-account-actions";
import {
  LandingBadge,
  LandingCard,
  LandingEyebrow,
  LandingLinkButton,
  LandingSectionHeading,
  landingContainerClassName,
  landingSectionClassName,
} from "./landing-primitives";

const trustItems = [
  {
    description: "Browse the public product catalog before signing in.",
    icon: PackageSearch,
    title: "Public product discovery",
  },
  {
    description: "Product visibility and eligibility can reflect membership.",
    icon: ShieldCheck,
    title: "Relevant access",
  },
  {
    description: "Signed-in customers see information and actions available to their account.",
    icon: LayoutDashboard,
    title: "Account-aware portal",
  },
];

const solutionItems = [
  {
    description:
      "Start with public information and the product catalog without creating an account.",
    icon: Search,
    title: "Explore openly",
  },
  {
    description:
      "Understand that product access and available actions can depend on membership and account status.",
    icon: BadgeCheck,
    title: "See what applies",
  },
  {
    description:
      "Use the portal to view orders, programme information and account actions available to you.",
    icon: ListChecks,
    title: "Continue with context",
  },
];

const participationOptions = [
  {
    description:
      "Membership status helps determine eligible products and the account actions available to a customer.",
    eyebrow: "Club access",
    icon: BadgeCheck,
    title: "Membership",
    tone: "warm",
  },
  {
    description:
      "AQGreen is a distinct programme with its own participation record and monthly commitments.",
    eyebrow: "Programme",
    icon: Leaf,
    title: "AQGreen",
    tone: "gold",
  },
  {
    description:
      "Onyx is a separate programme with its own participation and network placement records.",
    eyebrow: "Programme",
    icon: Network,
    title: "Onyx",
    tone: "violet",
  },
] as const;

const benefits = [
  {
    description: "Learn about the club and its products before deciding whether to create an account.",
    icon: Eye,
    title: "Clarity before commitment",
  },
  {
    description: "Account information and actions reflect the access available to each customer.",
    icon: SlidersHorizontal,
    title: "A more relevant view",
  },
  {
    description: "AQGreen and Onyx remain distinct programmes with their own participation records.",
    icon: ListChecks,
    title: "Clear programme boundaries",
  },
];

const journey = [
  {
    description: "Review the club, public information and available products.",
    title: "Explore",
  },
  {
    description:
      "Where self-registration is available, create an account to access information relevant to you.",
    title: "Create an account",
  },
  {
    description:
      "Sign in to view products, orders, programmes and actions available to your account.",
    title: "Use your portal",
  },
];

const faqs = [
  {
    answer:
      "Aqua Lifestyle Club is a wellness and membership platform that includes aQuathz products, programme participation and an area-based business network.",
    question: "What is Aqua Lifestyle Club?",
  },
  {
    answer:
      "No. You can learn about the club and browse the public product catalog before creating an account.",
    question: "Do I need an account to browse products?",
  },
  {
    answer:
      "No. Product visibility and eligibility can depend on membership. Your account shows the information and actions available to you.",
    question: "Does every customer see the same products?",
  },
  {
    answer:
      "No. AQGreen and Onyx are distinct programmes with separate participation records and rules.",
    question: "Are AQGreen and Onyx membership tiers?",
  },
  {
    answer:
      "Creating an account does not by itself activate programme participation. Access reflects your account status, role and Area settings.",
    question: "Does creating an account activate membership?",
  },
];

const iconToneClassNames = {
  gold: "bg-aqua-gold-soft text-aqua-gold-ink",
  violet: "bg-aqua-lavender-strong text-aqua-violet-ink",
  warm: "bg-aqua-cream text-aqua-navy",
} as const;

const IconTile = ({
  icon: Icon,
  tone = "violet",
}: {
  icon: LucideIcon;
  tone?: keyof typeof iconToneClassNames;
}) => (
  <span
    className={`flex size-10 shrink-0 items-center justify-center rounded-aqua-control ${iconToneClassNames[tone]}`}
  >
    <Icon aria-hidden="true" className="size-5" strokeWidth={1.75} />
  </span>
);

const ProductIndex = () => (
  <div
    aria-hidden="true"
    className="rounded-aqua-card border border-white/10 bg-aqua-navy p-6 sm:p-8"
  >
    <div className="flex items-start justify-between border-b border-white/10 pb-6">
      <div>
        <LandingEyebrow light>Product catalog</LandingEyebrow>
        <p className="mt-3 text-2xl font-semibold tracking-[-0.025em] text-white">aQuathz</p>
      </div>
      <span className="font-mono text-xs text-white/65">01 / 03</span>
    </div>
    <div className="mt-6 divide-y divide-white/10 border-y border-white/10">
      {["Water products", "Spraythz", "Health sets"].map((label, index) => (
        <div className="flex items-center justify-between py-4" key={label}>
          <span className="text-sm font-medium text-white">{label}</span>
          <span className="font-mono text-xs text-aqua-lavender-strong">0{index + 1}</span>
        </div>
      ))}
    </div>
  </div>
);

export const LandingPage = () => (
  <>
    <main className="overflow-x-clip bg-aqua-canvas text-aqua-ink">
      <section
        aria-labelledby="landing-title"
        className="relative isolate overflow-hidden bg-aqua-navy text-white"
      >
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-[radial-gradient(circle_at_82%_16%,rgba(108,59,216,0.24),transparent_34%),radial-gradient(circle_at_8%_92%,rgba(227,189,101,0.08),transparent_28%)]"
        />
        <div
          className={`${landingContainerClassName} relative grid gap-14 py-20 sm:py-24 lg:grid-cols-[1.05fr_0.95fr] lg:items-center lg:gap-16 lg:py-28`}
        >
          <div className="max-w-3xl">
            <LandingBadge>
              <ShieldCheck aria-hidden="true" className="size-4 text-aqua-gold" />
              Products, programmes and member access
            </LandingBadge>
            <h1
              className="mt-7 text-5xl font-semibold leading-[0.98] tracking-[-0.05em] text-balance sm:text-6xl lg:text-7xl"
              id="landing-title"
            >
              Live in health.
              <span className="block text-aqua-lavender-strong">Inspire to wealth.</span>
            </h1>
            <p className="mt-6 max-w-xl text-lg leading-8 text-aqua-dark-muted sm:text-xl">
              Explore aQuathz products, understand Aqua&apos;s participation programmes
              and access the information available to your account.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <LandingLinkButton href="/catalog">
                Browse products
                <ArrowRight aria-hidden="true" className="size-4" />
              </LandingLinkButton>
              <LandingLinkButton href="#solution" tone="secondary-dark">
                How Aqua works
              </LandingLinkButton>
            </div>
            <p className="mt-6 flex items-center gap-2 text-sm text-white/75">
              <CircleCheck aria-hidden="true" className="size-4 text-aqua-gold" />
              Browse the catalog before creating an account.
            </p>
          </div>

          <div className="mx-auto w-full max-w-md">
            <div className="rounded-aqua-feature border border-white/15 bg-aqua-navy-raised p-5 shadow-aqua-raised sm:p-7">
              <Image
                alt="Aqua Lifestyle Club"
                className="aspect-square w-full rounded-aqua-card object-cover"
                height={640}
                priority
                sizes="(min-width: 1024px) 390px, (min-width: 640px) 420px, calc(100vw - 80px)"
                src="/aqua-lifestyle-logo.jpg"
                width={640}
              />
            </div>
          </div>
        </div>
      </section>

      <section aria-labelledby="trust-title" className="border-b border-aqua-line bg-aqua-surface">
        <h2 className="sr-only" id="trust-title">What visitors can expect</h2>
        <div className={`${landingContainerClassName} grid divide-y divide-aqua-line md:grid-cols-3 md:divide-x md:divide-y-0`}>
          {trustItems.map((item) => (
            <div className="flex gap-4 py-7 md:px-6 md:first:pl-0 md:last:pr-0" key={item.title}>
              <item.icon aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-aqua-violet-dark" strokeWidth={1.75} />
              <div>
                <h3 className="text-sm font-semibold text-aqua-ink">{item.title}</h3>
                <p className="mt-1 text-sm leading-6 text-aqua-muted">{item.description}</p>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section aria-labelledby="problem-title">
        <div className={`${landingContainerClassName} ${landingSectionClassName} grid gap-8 lg:grid-cols-[0.65fr_1.35fr] lg:items-start`}>
          <LandingEyebrow>The challenge</LandingEyebrow>
          <div>
            <h2
              className="max-w-4xl text-4xl font-semibold leading-[1.08] tracking-[-0.04em] text-balance sm:text-5xl lg:text-6xl"
              id="problem-title"
            >
              Finding the right product, programme or next step should not require guesswork.
            </h2>
            <p className="mt-6 max-w-2xl text-base leading-7 text-aqua-muted sm:text-lg">
              Public product discovery, account access and programme participation serve
              different needs. Aqua gives each one a clear place without treating them as
              the same thing.
            </p>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="solution-title"
        className="scroll-mt-24 border-y border-aqua-line bg-aqua-surface"
        id="solution"
      >
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <LandingSectionHeading
            description="A straightforward experience that starts in public and becomes more relevant when you sign in."
            eyebrow="The Aqua experience"
            id="solution-title"
            title="Clear information at every stage."
          />
          <div className="mt-12 grid gap-5 lg:mt-14 lg:grid-cols-3">
            {solutionItems.map((item, index) => (
              <LandingCard className="flex h-full flex-col" key={item.title}>
                <div className="flex items-center justify-between">
                  <IconTile icon={item.icon} />
                  <span className="font-mono text-xs font-semibold text-aqua-muted">0{index + 1}</span>
                </div>
                <h3 className="mt-10 text-xl font-semibold tracking-[-0.02em]">{item.title}</h3>
                <p className="mt-3 text-sm leading-6 text-aqua-muted">{item.description}</p>
              </LandingCard>
            ))}
          </div>
        </div>
      </section>

      <section
        aria-labelledby="products-title"
        className="scroll-mt-24 bg-aqua-navy text-white"
        id="products"
      >
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <div className="grid gap-10 rounded-aqua-feature border border-white/10 bg-aqua-navy-raised p-6 sm:p-8 lg:grid-cols-[0.9fr_1.1fr] lg:items-center lg:p-10">
            <div className="lg:p-4">
              <LandingEyebrow light>Products</LandingEyebrow>
              <h2
                className="mt-4 max-w-xl text-3xl font-semibold leading-[1.1] tracking-[-0.035em] sm:text-4xl lg:text-5xl"
                id="products-title"
              >
                Explore the aQuathz product catalog.
              </h2>
              <p className="mt-5 max-w-xl text-base leading-7 text-aqua-dark-muted sm:text-lg">
                Browse water products, Spraythz and health sets. Product visibility and
                eligibility can depend on membership.
              </p>
              <LandingLinkButton className="mt-8" href="/catalog" tone="warm">
                Explore the catalog
                <ArrowRight aria-hidden="true" className="size-4" />
              </LandingLinkButton>
            </div>
            <ProductIndex />
          </div>
        </div>
      </section>

      <section
        aria-labelledby="participation-title"
        className="scroll-mt-24 border-t border-white/10 bg-aqua-navy text-white"
        id="programmes"
      >
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <LandingSectionHeading
            description="Membership, AQGreen and Onyx affect the customer experience in different ways. Programme participation remains separate from membership selection."
            eyebrow="Ways to participate"
            id="participation-title"
            light
            title="Distinct paths, clearly explained."
          />
          <div className="mt-12 grid gap-5 lg:mt-14 lg:grid-cols-3">
            {participationOptions.map((option, index) => (
              <article
                className="flex h-full flex-col rounded-aqua-card border border-white/10 bg-aqua-navy-raised p-6 sm:p-8"
                key={option.title}
              >
                <div className="flex items-center justify-between">
                  <IconTile icon={option.icon} tone={option.tone} />
                  <span className="font-mono text-xs font-semibold text-white/65">0{index + 1}</span>
                </div>
                <p className="mt-10 text-xs font-bold uppercase tracking-[0.16em] text-aqua-lavender-strong">
                  {option.eyebrow}
                </p>
                <h3 className="mt-3 text-xl font-semibold tracking-[-0.02em]">{option.title}</h3>
                <p className="mt-3 text-sm leading-6 text-aqua-dark-muted">{option.description}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="benefits-title">
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <LandingSectionHeading
            description="The value of the portal is not more information. It is clearer information in the right context."
            eyebrow="Practical benefits"
            id="benefits-title"
            title="Designed to reduce uncertainty."
          />
          <div className="mt-12 grid gap-5 lg:mt-14 lg:grid-cols-3">
            {benefits.map((benefit) => (
              <LandingCard className="h-full" key={benefit.title}>
                <IconTile icon={benefit.icon} />
                <h3 className="mt-8 text-xl font-semibold tracking-[-0.02em]">{benefit.title}</h3>
                <p className="mt-3 text-sm leading-6 text-aqua-muted">{benefit.description}</p>
              </LandingCard>
            ))}
          </div>
        </div>
      </section>

      <section
        aria-labelledby="journey-title"
        className="scroll-mt-24 border-y border-aqua-line bg-aqua-surface"
        id="how-it-works"
      >
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <LandingSectionHeading
            description="Move from public discovery to account-specific information without losing context."
            eyebrow="How it works"
            id="journey-title"
            title="A clear route into Aqua."
          />
          <ol className="mt-12 overflow-hidden rounded-aqua-card border border-aqua-line lg:mt-14">
            {journey.map((step, index) => (
              <li
                className="grid gap-4 border-b border-aqua-line bg-aqua-surface p-6 last:border-b-0 sm:grid-cols-[4rem_0.6fr_1fr] sm:items-center sm:p-8"
                key={step.title}
              >
                <span className="font-mono text-sm font-semibold text-aqua-violet-dark">0{index + 1}</span>
                <h3 className="text-lg font-semibold tracking-[-0.015em]">{step.title}</h3>
                <p className="text-sm leading-6 text-aqua-muted">{step.description}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section aria-labelledby="community-title" className="bg-aqua-lavender">
        <div className={`${landingContainerClassName} ${landingSectionClassName} grid gap-10 lg:grid-cols-[0.85fr_1.15fr] lg:items-center lg:gap-16`}>
          <div className="flex min-h-80 flex-col justify-between rounded-aqua-feature bg-aqua-navy-raised p-8 text-white sm:min-h-96 sm:p-10">
            <Users aria-hidden="true" className="size-10 text-aqua-gold" strokeWidth={1.75} />
            <p className="max-w-sm text-3xl font-semibold leading-[1.1] tracking-[-0.03em] sm:text-4xl">
              A model designed for local connection.
            </p>
          </div>
          <div>
            <LandingSectionHeading
              description="Aqua's area-based business model includes Area Leaders and Facilitators alongside customer membership and programme participation. These roles and programmes remain distinct."
              eyebrow="Community model"
              id="community-title"
              title="Local roles within a wider club."
            />
            <div className="mt-8 flex items-start gap-4 border-t border-aqua-violet/20 pt-6">
              <ShieldCheck aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-aqua-violet-dark" />
              <p className="max-w-xl text-sm leading-6 text-aqua-muted">
                Account access reflects the customer&apos;s status and role without merging
                separate programme records or responsibilities.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="faq-title"
        className="scroll-mt-24"
        id="faq"
      >
        <div className={`${landingContainerClassName} ${landingSectionClassName} grid gap-12 lg:grid-cols-[0.7fr_1.3fr] lg:gap-20`}>
          <LandingSectionHeading
            description="The essentials about public browsing, accounts and programme participation."
            eyebrow="FAQ"
            id="faq-title"
            title="Questions before joining."
          />
          <div className="border-t border-aqua-line">
            {faqs.map((faq, index) => (
              <details className="group border-b border-aqua-line" key={faq.question}>
                <summary className="flex min-h-20 cursor-pointer list-none items-center justify-between gap-5 py-5 font-semibold marker:content-none">
                  <span className="flex items-start gap-4">
                    <span className="mt-0.5 hidden font-mono text-xs font-semibold text-aqua-muted sm:inline">
                      0{index + 1}
                    </span>
                    {faq.question}
                  </span>
                  <span
                    aria-hidden="true"
                    className="flex size-8 shrink-0 items-center justify-center rounded-full border border-aqua-line text-lg font-normal transition-transform group-open:rotate-45"
                  >
                    +
                  </span>
                </summary>
                <p className="max-w-2xl pb-6 text-sm leading-6 text-aqua-muted sm:pl-10">
                  {faq.answer}
                </p>
              </details>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="join-title" className="bg-aqua-navy text-white">
        <div className={`${landingContainerClassName} ${landingSectionClassName}`}>
          <div className="mx-auto max-w-4xl text-center">
            <LandingEyebrow light>Your next step</LandingEyebrow>
            <h2
              className="mt-4 text-4xl font-semibold leading-[1.08] tracking-[-0.04em] text-balance sm:text-5xl lg:text-6xl"
              id="join-title"
            >
              Explore what is available to you.
            </h2>
            <p className="mx-auto mt-6 max-w-2xl text-base leading-7 text-aqua-dark-muted sm:text-lg">
              Browse the public catalog, create an account where registration is
              available, or sign in to continue.
            </p>
            <LandingAccountActions />
          </div>
        </div>
      </section>
    </main>

    <footer className="bg-aqua-navy text-aqua-dark-muted">
      <div className={`${landingContainerClassName} grid gap-10 py-12 sm:grid-cols-[1fr_auto] sm:items-end`}>
        <div>
          <Link className="inline-flex items-center gap-4 text-white" href="/">
            <Image
              alt=""
              aria-hidden="true"
              className="size-14 rounded-aqua-control object-cover"
              height={56}
              src="/aqua-lifestyle-logo.jpg"
              width={56}
            />
            <span>
              <span className="block text-base font-semibold">Aqua Lifestyle Club</span>
              <span className="mt-1 block text-xs text-white/65">Always connected</span>
            </span>
          </Link>
          <p className="mt-6 max-w-md text-sm leading-6 text-white/65">
            Products, programmes and member access in one considered experience.
          </p>
        </div>
        <nav aria-label="Footer navigation" className="flex flex-wrap gap-x-6 gap-y-3 text-sm">
          <Link className="transition-colors hover:text-white" href="/#solution">Why Aqua</Link>
          <Link className="transition-colors hover:text-white" href="/#products">Products</Link>
          <Link className="transition-colors hover:text-white" href="/#programmes">Programmes</Link>
          <Link className="transition-colors hover:text-white" href="/contact">Contact</Link>
        </nav>
      </div>
    </footer>
  </>
);
