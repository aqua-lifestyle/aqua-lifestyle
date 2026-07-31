import {
  ArrowRight,
  BadgeCheck,
  CircleCheck,
  Droplets,
  HeartPulse,
  Leaf,
  Network,
  Package,
  ShieldCheck,
  Sparkles,
  Users,
} from "lucide-react";
import Link from "next/link";
import type { LucideIcon } from "lucide-react";

import { LinkButton } from "@/src/shared/ui";

import { LandingAccountActions } from "./landing-account-actions";

const valuePillars = [
  {
    description:
      "Discover aQuathz products and see how product access connects to membership.",
    icon: HeartPulse,
    title: "Health",
  },
  {
    description:
      "Keep products, programme information and member activity together in one place.",
    icon: Sparkles,
    title: "Lifestyle",
  },
  {
    description:
      "Stay connected through the club's Area Leader and Facilitator network.",
    icon: Users,
    title: "Community",
  },
  {
    description:
      "Explore pathways for participation, leadership and growing a local network.",
    icon: Network,
    title: "Opportunity",
  },
];

const journey = [
  {
    description:
      "Learn about the club, its programmes and available products before creating an account.",
    title: "Explore your options",
  },
  {
    description:
      "Create an account to access the member experience and information relevant to you.",
    title: "Join the club",
  },
  {
    description:
      "Use your dashboard to follow products, orders, programmes and your club activity.",
    title: "Manage your journey",
  },
];

const pathways = [
  {
    description:
      "The starting point for club access, eligible products and ongoing member activity.",
    eyebrow: "Membership",
    icon: BadgeCheck,
    title: "A connected member experience",
  },
  {
    description:
      "A programme pathway centred on participation, monthly commitments and savings activity.",
    eyebrow: "AQGreen",
    icon: Leaf,
    title: "Structured participation",
  },
  {
    description:
      "A pathway that connects network participation with Facilitator and Area Leader roles.",
    eyebrow: "Onyx",
    icon: Network,
    title: "Community opportunity",
  },
];

const faqs = [
  {
    answer:
      "Aqua Lifestyle Club is a membership platform that brings together aQuathz products, member programmes and an area-based community network.",
    question: "What is Aqua Lifestyle Club?",
  },
  {
    answer:
      "No. You can learn about the club and browse the public product catalog before deciding to create an account.",
    question: "Do I need an account to browse products?",
  },
  {
    answer:
      "Product visibility and eligibility can depend on membership. Your account shows the information and actions available to you.",
    question: "Does every member see the same products?",
  },
  {
    answer:
      "Signed-in members use their dashboard to view the products, orders, programmes and club activity available for their role.",
    question: "Where do I manage my membership activity?",
  },
];

type SectionHeadingProps = {
  align?: "center" | "left";
  description: string;
  eyebrow: string;
  id: string;
  title: string;
};

const SectionHeading = ({
  align = "left",
  description,
  eyebrow,
  id,
  title,
}: SectionHeadingProps) => (
  <div className={align === "center" ? "mx-auto max-w-2xl text-center" : "max-w-2xl"}>
    <p className="text-xs font-bold uppercase tracking-[0.22em] text-teal-700">
      {eyebrow}
    </p>
    <h2
      className="mt-4 text-3xl font-bold tracking-[-0.035em] text-slate-950 sm:text-4xl"
      id={id}
    >
      {title}
    </h2>
    <p className="mt-4 text-base leading-7 text-slate-600 sm:text-lg">
      {description}
    </p>
  </div>
);

const ValueCard = ({
  description,
  icon: Icon,
  title,
}: {
  description: string;
  icon: LucideIcon;
  title: string;
}) => (
  <article className="group border-t border-slate-200 pt-6">
    <div className="flex size-11 items-center justify-center rounded-full bg-teal-50 text-teal-700 transition-transform duration-300 group-hover:-translate-y-1">
      <Icon aria-hidden="true" className="size-5" strokeWidth={1.8} />
    </div>
    <h3 className="mt-5 text-lg font-bold text-slate-950">{title}</h3>
    <p className="mt-2 text-sm leading-6 text-slate-600">{description}</p>
  </article>
);

export const LandingPage = () => (
  <main className="overflow-hidden bg-[#fbfcfa] text-slate-950">
    <section
      aria-labelledby="landing-title"
      className="relative isolate border-b border-white/10 bg-[#082f35] text-white"
    >
      <div
        aria-hidden="true"
        className="absolute -right-48 -top-56 size-[34rem] rounded-full border border-teal-200/15 bg-teal-300/10 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-52 left-1/3 size-[30rem] rounded-full bg-cyan-300/10 blur-3xl"
      />
      <div className="relative mx-auto grid min-h-[calc(100svh-4rem)] max-w-7xl items-center gap-14 px-4 py-20 sm:px-6 lg:grid-cols-[1.12fr_0.88fr] lg:px-8 lg:py-24">
        <div className="max-w-3xl">
          <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1.5 text-xs font-semibold tracking-wide text-teal-100">
            <ShieldCheck aria-hidden="true" className="size-4" />
            Membership | Products | Programmes
          </div>
          <h1
            className="mt-7 text-5xl font-bold leading-[0.98] tracking-[-0.055em] text-balance sm:text-6xl lg:text-7xl"
            id="landing-title"
          >
            Live well. Grow together.
          </h1>
          <p className="mt-7 max-w-2xl text-lg leading-8 text-slate-200 sm:text-xl">
            Aqua Lifestyle Club brings membership, aQuathz products, community
            programmes and area-based support into one connected experience.
          </p>
          <div className="mt-9 flex flex-col gap-3 sm:flex-row">
            <LinkButton
              className="bg-teal-300 text-slate-950 shadow-none hover:bg-teal-200"
              href="#value"
              size="lg"
            >
              Explore the club
              <ArrowRight aria-hidden="true" className="size-4" />
            </LinkButton>
            <LinkButton
              className="border-white/20 bg-white/5 text-white hover:bg-white/10"
              href="/catalog"
              size="lg"
              variant="outline"
            >
              Browse products
            </LinkButton>
          </div>
          <p className="mt-6 flex items-center gap-2 text-sm text-slate-300">
            <CircleCheck aria-hidden="true" className="size-4 text-teal-300" />
            Explore the value before you decide to join.
          </p>
        </div>

        <div aria-hidden="true" className="relative mx-auto hidden w-full max-w-md lg:block">
          <div className="aspect-square rounded-full border border-white/10 p-7">
            <div className="relative flex size-full items-center justify-center overflow-hidden rounded-full bg-gradient-to-br from-teal-200 via-cyan-300 to-teal-600 shadow-[0_32px_90px_rgba(45,212,191,0.22)]">
              <div className="absolute inset-8 rounded-full border border-white/30" />
              <div className="absolute inset-16 rounded-full border border-white/25" />
              <Droplets className="size-28 text-[#08333a]" strokeWidth={1.2} />
            </div>
          </div>
          <div className="absolute -bottom-3 -left-6 w-52 rounded-2xl border border-white/15 bg-[#0b3b42]/90 p-4 shadow-2xl backdrop-blur">
            <p className="text-xs uppercase tracking-[0.18em] text-teal-200">One club</p>
            <p className="mt-2 text-sm font-semibold leading-5 text-white">
              Health, lifestyle, community and opportunity.
            </p>
          </div>
        </div>
      </div>
    </section>

    <section
      aria-labelledby="value-title"
      className="scroll-mt-20 px-4 py-20 sm:px-6 sm:py-24 lg:px-8"
      id="value"
    >
      <div className="mx-auto max-w-7xl">
        <SectionHeading
          description="Aqua connects the practical parts of club membership without losing sight of the people and communities behind it."
          eyebrow="Why Aqua"
          id="value-title"
          title="More than a membership account"
        />
        <div className="mt-12 grid gap-x-8 gap-y-10 sm:grid-cols-2 lg:grid-cols-4">
          {valuePillars.map((pillar) => (
            <ValueCard key={pillar.title} {...pillar} />
          ))}
        </div>
      </div>
    </section>

    <section
      aria-labelledby="journey-title"
      className="scroll-mt-20 bg-[#eef7f3] px-4 py-20 sm:px-6 sm:py-24 lg:px-8"
      id="how-it-works"
    >
      <div className="mx-auto max-w-7xl">
        <SectionHeading
          align="center"
          description="Start with the information you need, then move into a member experience shaped around your access."
          eyebrow="How it works"
          id="journey-title"
          title="A clear path from discovery to participation"
        />
        <ol className="mt-14 grid gap-5 lg:grid-cols-3">
          {journey.map((step, index) => (
            <li
              className="relative rounded-2xl border border-teal-950/10 bg-white p-7 shadow-[0_16px_45px_rgba(15,71,70,0.06)]"
              key={step.title}
            >
              <span className="text-sm font-bold text-teal-700">0{index + 1}</span>
              <h3 className="mt-8 text-xl font-bold tracking-tight text-slate-950">
                {step.title}
              </h3>
              <p className="mt-3 text-sm leading-6 text-slate-600">{step.description}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>

    <section
      aria-labelledby="pathways-title"
      className="scroll-mt-20 px-4 py-20 sm:px-6 sm:py-24 lg:px-8"
      id="programmes"
    >
      <div className="mx-auto max-w-7xl">
        <SectionHeading
          description="The club brings membership and programme pathways into one platform while keeping each journey distinct."
          eyebrow="Membership overview"
          id="pathways-title"
          title="Find the path that fits your journey"
        />
        <div className="mt-12 grid overflow-hidden rounded-3xl border border-slate-200 bg-white lg:grid-cols-3">
          {pathways.map((pathway, index) => (
            <article
              className={`p-7 sm:p-9 ${index > 0 ? "border-t border-slate-200 lg:border-l lg:border-t-0" : ""}`}
              key={pathway.eyebrow}
            >
              <pathway.icon aria-hidden="true" className="size-7 text-teal-700" strokeWidth={1.7} />
              <p className="mt-8 text-xs font-bold uppercase tracking-[0.2em] text-teal-700">
                {pathway.eyebrow}
              </p>
              <h3 className="mt-3 text-xl font-bold text-slate-950">{pathway.title}</h3>
              <p className="mt-3 text-sm leading-6 text-slate-600">{pathway.description}</p>
            </article>
          ))}
        </div>
      </div>
    </section>

    <section
      aria-labelledby="products-title"
      className="px-4 pb-20 sm:px-6 sm:pb-24 lg:px-8"
    >
      <div className="mx-auto grid max-w-7xl overflow-hidden rounded-3xl bg-[#0b3940] text-white lg:grid-cols-[0.88fr_1.12fr]">
        <div className="relative min-h-72 overflow-hidden bg-teal-300 p-8 text-[#082f35] sm:p-12">
          <div aria-hidden="true" className="absolute -bottom-24 -right-14 size-72 rounded-full border-[44px] border-white/25" />
          <Package aria-hidden="true" className="relative size-14" strokeWidth={1.4} />
          <p className="relative mt-20 max-w-xs text-sm font-bold uppercase tracking-[0.18em]">
            aQuathz product catalog
          </p>
        </div>
        <div className="p-8 sm:p-12 lg:p-14">
          <p className="text-xs font-bold uppercase tracking-[0.22em] text-teal-200">Products</p>
          <h2 className="mt-4 text-3xl font-bold tracking-tight sm:text-4xl" id="products-title">
            See what is available before you join
          </h2>
          <p className="mt-5 max-w-xl leading-7 text-slate-200">
            Browse the public catalog for aQuathz water products, Spraythz and health
            sets. Product visibility and eligibility can depend on membership.
          </p>
          <LinkButton
            className="mt-8 border-white/20 bg-white text-slate-950 hover:bg-teal-50"
            href="/catalog"
            size="lg"
            variant="outline"
          >
            Explore the catalog
            <ArrowRight aria-hidden="true" className="size-4" />
          </LinkButton>
        </div>
      </div>
    </section>

    <section
      aria-labelledby="faq-title"
      className="scroll-mt-20 border-y border-slate-200 bg-white px-4 py-20 sm:px-6 sm:py-24 lg:px-8"
      id="faq"
    >
      <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[0.75fr_1.25fr]">
        <SectionHeading
          description="Straightforward answers to help you understand the public and member experience."
          eyebrow="FAQ"
          id="faq-title"
          title="Questions before joining"
        />
        <div className="divide-y divide-slate-200 border-y border-slate-200">
          {faqs.map((faq) => (
            <details className="group py-6" key={faq.question}>
              <summary className="flex cursor-pointer list-none items-center justify-between gap-6 font-bold text-slate-950 marker:content-none">
                {faq.question}
                <span
                  aria-hidden="true"
                  className="flex size-7 shrink-0 items-center justify-center rounded-full border border-slate-300 text-lg font-normal transition-transform group-open:rotate-45"
                >
                  +
                </span>
              </summary>
              <p className="max-w-2xl pt-4 text-sm leading-6 text-slate-600">{faq.answer}</p>
            </details>
          ))}
        </div>
      </div>
    </section>

    <section
      aria-labelledby="join-title"
      className="bg-[#082f35] px-4 py-20 text-white sm:px-6 sm:py-24 lg:px-8"
    >
      <div className="mx-auto max-w-3xl text-center">
        <p className="text-xs font-bold uppercase tracking-[0.22em] text-teal-200">Your next step</p>
        <h2 className="mt-4 text-4xl font-bold tracking-[-0.04em] sm:text-5xl" id="join-title">
          Ready to take a closer look?
        </h2>
        <p className="mx-auto mt-5 max-w-2xl text-lg leading-8 text-slate-200">
          Create your account to begin your Aqua journey, or return to the dashboard if
          you are already a member.
        </p>
        <LandingAccountActions />
      </div>
    </section>

    <footer className="bg-[#061f24] px-4 py-10 text-slate-300 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-8 sm:flex-row sm:items-center sm:justify-between">
        <Link className="inline-flex items-center gap-3 text-white" href="/">
          <span className="flex size-9 items-center justify-center rounded-full bg-teal-300 text-[#082f35]">
            <Droplets aria-hidden="true" className="size-5" />
          </span>
          <span className="font-bold">Aqua Lifestyle Club</span>
        </Link>
        <nav aria-label="Footer navigation" className="flex flex-wrap gap-x-6 gap-y-3 text-sm">
          <Link className="hover:text-white" href="/#value">Why Aqua</Link>
          <Link className="hover:text-white" href="/#programmes">Programmes</Link>
          <Link className="hover:text-white" href="/catalog">Catalog</Link>
          <Link className="hover:text-white" href="/contact">Contact</Link>
        </nav>
      </div>
      <p className="mx-auto mt-8 max-w-7xl border-t border-white/10 pt-6 text-xs text-slate-400">
        Copyright {new Date().getFullYear()} Aqua Lifestyle Club.
      </p>
    </footer>
  </main>
);
