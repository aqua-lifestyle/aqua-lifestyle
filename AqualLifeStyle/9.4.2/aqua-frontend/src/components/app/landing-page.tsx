import type { LucideIcon } from "lucide-react";
import {
  ArrowRight,
  BadgeCheck,
  CircleCheck,
  HeartPulse,
  Leaf,
  Network,
  ShieldCheck,
  Sparkles,
  Users,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";

import { LinkButton } from "@/src/shared/ui";

import { LandingAccountActions } from "./landing-account-actions";

const pillars = [
  {
    description:
      "Discover aQuathz products and understand how access can connect to membership.",
    icon: HeartPulse,
    title: "Health",
  },
  {
    description:
      "Keep products, programme information and member activity in one experience.",
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
      "Explore participation and leadership pathways within a local network.",
    icon: Network,
    title: "Opportunity",
  },
];

const pathways = [
  {
    accentClassName: "bg-[#f7e3ca] text-[#111044]",
    description:
      "The starting point for club access, eligible products and ongoing member activity.",
    eyebrow: "Membership",
    icon: BadgeCheck,
    title: "A connected member experience",
  },
  {
    accentClassName: "bg-[#f4e4b8] text-[#594000]",
    description:
      "A distinct pathway centred on participation and monthly commitments.",
    eyebrow: "AQGreen",
    icon: Leaf,
    title: "Structured participation",
  },
  {
    accentClassName: "bg-[#ded3ff] text-[#351077]",
    description:
      "A pathway connecting network participation with Facilitator and Area Leader roles.",
    eyebrow: "Onyx",
    icon: Network,
    title: "Community opportunity",
  },
];

const journey = [
  {
    description:
      "Learn about the club, its pathways and available products before creating an account.",
    title: "Explore",
  },
  {
    description:
      "Create an account to access the member experience and information relevant to you.",
    title: "Join",
  },
  {
    description:
      "Follow products, orders, programmes and club activity from your dashboard.",
    title: "Participate",
  },
];

const faqs = [
  {
    answer:
      "Aqua Lifestyle Club is a membership platform bringing together aQuathz products, member programmes and an area-based community network.",
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
  light?: boolean;
  title: string;
};

const SectionHeading = ({
  align = "left",
  description,
  eyebrow,
  id,
  light = false,
  title,
}: SectionHeadingProps) => (
  <div className={align === "center" ? "mx-auto max-w-3xl text-center" : "max-w-3xl"}>
    <p
      className={`text-sm font-bold uppercase tracking-[0.18em] ${
        light ? "text-[#cdb8ff]" : "text-[#6424d0]"
      }`}
    >
      {eyebrow}
    </p>
    <h2
      className={`mt-4 text-3xl font-semibold leading-[1.08] tracking-[-0.04em] text-balance sm:text-4xl lg:text-5xl ${
        light ? "text-white" : "text-[#171326]"
      }`}
      id={id}
    >
      {title}
    </h2>
    <p
      className={`mt-5 max-w-2xl text-base leading-7 sm:text-lg ${
        align === "center" ? "mx-auto" : ""
      } ${light ? "text-[#d7d5e4]" : "text-[#615b69]"}`}
    >
      {description}
    </p>
  </div>
);

const PillarCard = ({
  description,
  icon: Icon,
  index,
  title,
}: {
  description: string;
  icon: LucideIcon;
  index: number;
  title: string;
}) => (
  <article className="flex h-full flex-col rounded-3xl border border-[#ddd5cb] bg-white/55 p-6 shadow-[0_18px_55px_rgba(38,28,53,0.05)] sm:p-7">
    <div className="flex items-center justify-between">
      <span className="font-mono text-xs font-semibold text-[#827889]">0{index + 1}</span>
      <span className="flex size-11 items-center justify-center rounded-2xl bg-[#eee7ff] text-[#6424d0]">
        <Icon aria-hidden="true" className="size-5" strokeWidth={1.7} />
      </span>
    </div>
    <h3 className="mt-10 text-xl font-semibold tracking-[-0.025em] text-[#171326]">
      {title}
    </h3>
    <p className="mt-3 text-sm leading-6 text-[#615b69]">{description}</p>
  </article>
);

const ProductVisual = () => (
  <div
    aria-hidden="true"
    className="relative flex min-h-72 flex-col justify-between overflow-hidden rounded-2xl border border-white/10 bg-[#100a2e] p-6 sm:min-h-96 sm:p-8"
  >
    <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(117,64,232,0.18),transparent_55%)]" />
    <div className="relative flex items-start justify-between border-b border-white/10 pb-6">
      <div>
        <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-[#e3bd65]">
          Product catalog
        </p>
        <p className="mt-2 text-2xl font-semibold tracking-[-0.03em] text-white">
          aQuathz
        </p>
      </div>
      <span className="font-mono text-xs text-white/40">01 / 03</span>
    </div>
    <div className="relative mt-8 divide-y divide-white/10 border-y border-white/10">
      {[
        ["01", "Water products"],
        ["02", "Spraythz"],
        ["03", "Health sets"],
      ].map(([number, label]) => (
        <div className="flex items-center justify-between py-4" key={number}>
          <span className="text-sm font-medium text-white">{label}</span>
          <span className="font-mono text-xs text-[#cdb8ff]">{number}</span>
        </div>
      ))}
    </div>
  </div>
);

export const LandingPage = () => (
  <>
    <main className="overflow-x-clip bg-[#fbf6ee] text-[#171326]">
      <section
        aria-labelledby="landing-title"
        className="relative isolate overflow-hidden bg-[#05051f] text-white"
      >
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-[radial-gradient(circle_at_85%_15%,rgba(110,45,225,0.26),transparent_34%),radial-gradient(circle_at_10%_90%,rgba(227,189,101,0.12),transparent_30%)]"
        />
        <div className="relative mx-auto grid max-w-7xl gap-14 px-5 py-16 sm:px-8 sm:py-20 lg:px-10 lg:py-24 xl:grid-cols-[1.02fr_0.98fr] xl:items-center xl:gap-20 xl:py-28">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/[0.06] px-4 py-2 text-xs font-semibold text-white/80">
              <ShieldCheck aria-hidden="true" className="size-4 text-[#cdb8ff]" />
              Membership, products and community
            </div>
            <h1
              className="mt-7 text-5xl font-semibold leading-[0.98] tracking-[-0.055em] text-balance sm:text-6xl lg:text-7xl"
              id="landing-title"
            >
              Live in health.
              <span className="block text-[#cdb8ff]">Inspire to wealth.</span>
            </h1>
            <p className="mt-6 max-w-xl text-lg leading-8 text-[#d7d5e4] sm:text-xl">
              A connected club experience bringing together aQuathz products,
              membership pathways and area-based community support.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <LinkButton
                className="rounded-full border-[#7d49e8] bg-[#7540e8] px-7 text-white shadow-none hover:border-[#8b5aef] hover:bg-[#8655ef]"
                href="#value"
                size="lg"
              >
                Discover Aqua
                <ArrowRight aria-hidden="true" className="size-4" />
              </LinkButton>
              <LinkButton
                className="rounded-full border-white/20 bg-transparent px-7 text-white hover:border-white/35 hover:bg-white/10"
                href="/catalog"
                size="lg"
                variant="outline"
              >
                Browse products
              </LinkButton>
            </div>
            <p className="mt-6 flex items-center gap-2 text-sm text-white/70">
              <CircleCheck aria-hidden="true" className="size-4 text-[#e3bd65]" />
              Explore before you decide to join.
            </p>
          </div>

          <div className="relative mx-auto w-full max-w-xl">
            <div
              aria-hidden="true"
              className="absolute inset-x-4 top-12 h-48 rotate-[-7deg] rounded-[50%] bg-gradient-to-r from-[#30106f] via-[#6725d5] to-[#8c56ed] sm:inset-x-0 sm:h-60"
            />
            <div className="relative mx-auto max-w-md rounded-[2rem] border border-white/15 bg-[#0d0c35]/90 p-5 shadow-[0_36px_90px_rgba(0,0,0,0.4)] sm:p-7">
              <Image
                alt="Aqua Lifestyle Club"
                className="aspect-square w-full rounded-2xl object-cover"
                height={640}
                priority
                sizes="(min-width: 1280px) 390px, (min-width: 640px) 420px, calc(100vw - 80px)"
                src="/aqua-lifestyle-logo.jpg"
                width={640}
              />
              <div className="grid grid-cols-2 gap-2 pt-5 sm:grid-cols-4">
                {pillars.map((pillar) => (
                  <span
                    className="rounded-full border border-white/10 bg-white/[0.06] px-3 py-2 text-center text-xs font-semibold text-white/80"
                    key={pillar.title}
                  >
                    {pillar.title}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </div>
      </section>

      <section aria-labelledby="problem-title" className="border-b border-[#ddd5cb]">
        <div className="mx-auto grid max-w-7xl gap-8 px-5 py-20 sm:px-8 sm:py-24 lg:grid-cols-[0.65fr_1.35fr] lg:items-end lg:px-10 lg:py-28">
          <p className="text-sm font-bold uppercase tracking-[0.18em] text-[#6424d0]">
            The everyday challenge
          </p>
          <div>
            <h2
              className="max-w-4xl text-4xl font-semibold leading-[1.06] tracking-[-0.045em] text-balance sm:text-5xl lg:text-6xl"
              id="problem-title"
            >
              Products, participation and support should feel connected.
            </h2>
            <p className="mt-6 max-w-2xl text-base leading-7 text-[#615b69] sm:text-lg">
              Aqua brings the practical parts of club membership into one place,
              while keeping every pathway distinct.
            </p>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="value-title"
        className="scroll-mt-24 px-5 py-20 sm:px-8 sm:py-24 lg:px-10 lg:py-28"
        id="value"
      >
        <div className="mx-auto max-w-7xl">
          <SectionHeading
            description="A membership experience designed around the things that matter to everyday club life."
            eyebrow="Why Aqua"
            id="value-title"
            title="One club. Four connected ideas."
          />
          <div className="mt-12 grid gap-4 sm:grid-cols-2 lg:mt-14 lg:grid-cols-4">
            {pillars.map((pillar, index) => (
              <PillarCard index={index} key={pillar.title} {...pillar} />
            ))}
          </div>
        </div>
      </section>

      <section
        aria-labelledby="pathways-title"
        className="scroll-mt-24 bg-[#080722] px-5 py-20 text-white sm:px-8 sm:py-24 lg:px-10 lg:py-28"
        id="programmes"
      >
        <div className="mx-auto max-w-7xl">
          <SectionHeading
            description="Explore each pathway first. Your account shows the access and actions available to you."
            eyebrow="Membership overview"
            id="pathways-title"
            light
            title="Different pathways. One connected experience."
          />
          <div className="mt-12 grid gap-5 lg:mt-14 lg:grid-cols-3">
            {pathways.map((pathway, index) => (
              <article
                className="flex h-full flex-col rounded-3xl border border-white/10 bg-[#121030] p-7 shadow-[0_24px_70px_rgba(0,0,0,0.16)] sm:p-8"
                key={pathway.eyebrow}
              >
                <div className="flex items-center justify-between gap-4">
                  <span
                    className={`flex size-12 items-center justify-center rounded-2xl ${pathway.accentClassName}`}
                  >
                    <pathway.icon aria-hidden="true" className="size-6" strokeWidth={1.6} />
                  </span>
                  <span className="font-mono text-sm font-semibold text-white/45">
                    0{index + 1}
                  </span>
                </div>
                <p className="mt-12 text-xs font-bold uppercase tracking-[0.18em] text-[#cdb8ff]">
                  {pathway.eyebrow}
                </p>
                <h3 className="mt-3 text-2xl font-semibold leading-tight tracking-[-0.025em]">
                  {pathway.title}
                </h3>
                <p className="mt-4 text-sm leading-6 text-[#d7d5e4]">
                  {pathway.description}
                </p>
              </article>
            ))}
          </div>

          <div
            className="mt-16 grid gap-8 rounded-[2rem] border border-white/10 bg-[#21104d] p-5 sm:p-8 lg:mt-20 lg:grid-cols-[0.9fr_1.1fr] lg:items-center lg:p-10"
            id="products"
          >
            <div className="px-1 py-5 sm:px-3 lg:p-6">
              <p className="text-sm font-bold uppercase tracking-[0.18em] text-[#e3bd65]">
                Featured products
              </p>
              <h2 className="mt-4 max-w-xl text-3xl font-semibold leading-[1.08] tracking-[-0.04em] sm:text-4xl lg:text-5xl">
                See the product world behind the club.
              </h2>
              <p className="mt-5 max-w-xl text-base leading-7 text-[#d7d5e4]">
                Explore aQuathz water products, Spraythz and health sets.
                Availability and eligibility can depend on membership.
              </p>
              <LinkButton
                className="mt-8 rounded-full border-[#f7e3ca] bg-[#f7e3ca] px-7 text-[#111044] hover:border-white hover:bg-white"
                href="/catalog"
                size="lg"
                variant="outline"
              >
                Explore the catalog
                <ArrowRight aria-hidden="true" className="size-4" />
              </LinkButton>
            </div>
            <ProductVisual />
          </div>
        </div>
      </section>

      <section
        aria-labelledby="journey-title"
        className="scroll-mt-24 px-5 py-20 sm:px-8 sm:py-24 lg:px-10 lg:py-28"
        id="how-it-works"
      >
        <div className="mx-auto max-w-7xl">
          <SectionHeading
            description="Start with clear information, then move into an experience shaped around your access."
            eyebrow="How it works"
            id="journey-title"
            title="A clear path into the club."
          />
          <ol className="mt-12 overflow-hidden rounded-3xl border border-[#d8d0c6] bg-white/45 lg:mt-14">
            {journey.map((step, index) => (
              <li
                className="grid gap-4 border-b border-[#d8d0c6] p-6 last:border-b-0 sm:grid-cols-[4rem_0.55fr_1fr] sm:items-center sm:p-7 lg:p-8"
                key={step.title}
              >
                <span className="font-mono text-sm font-semibold text-[#6424d0]">
                  0{index + 1}
                </span>
                <h3 className="text-xl font-semibold tracking-[-0.02em]">{step.title}</h3>
                <p className="text-sm leading-6 text-[#615b69]">{step.description}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section aria-labelledby="community-title" className="bg-[#f0ecf7] px-5 py-20 sm:px-8 sm:py-24 lg:px-10 lg:py-28">
        <div className="mx-auto grid max-w-7xl gap-10 lg:grid-cols-[0.85fr_1.15fr] lg:items-center lg:gap-16">
          <div className="relative flex min-h-80 flex-col justify-between overflow-hidden rounded-[2rem] bg-[#21104d] p-8 text-white sm:min-h-96 sm:p-10">
            <Users aria-hidden="true" className="size-12 text-[#e3bd65]" strokeWidth={1.4} />
            <div aria-hidden="true" className="absolute bottom-0 right-0 h-24 w-40 border-l border-t border-white/10" />
            <p className="relative max-w-sm text-3xl font-semibold leading-[1.08] tracking-[-0.035em] sm:text-4xl">
              People are the heart of the experience.
            </p>
          </div>
          <div>
            <SectionHeading
              description="Aqua's area-based model connects members with Area Leaders and Facilitators, bringing local support into the wider club experience."
              eyebrow="Community"
              id="community-title"
              title="Belong locally. Stay connected to the whole."
            />
            <div className="mt-8 flex items-start gap-4 border-t border-[#6424d0]/20 pt-6">
              <ShieldCheck aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-[#6424d0]" />
              <p className="max-w-xl text-sm leading-6 text-[#514860]">
                Programme participation, roles and available actions remain visible
                through the member dashboard.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="faq-title"
        className="scroll-mt-24 px-5 py-20 sm:px-8 sm:py-24 lg:px-10 lg:py-28"
        id="faq"
      >
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[0.7fr_1.3fr] lg:gap-20">
          <SectionHeading
            description="Clear answers about the public and member experience."
            eyebrow="FAQ"
            id="faq-title"
            title="Questions before joining."
          />
          <div className="border-t border-[#cfc5d2]">
            {faqs.map((faq, index) => (
              <details className="group border-b border-[#cfc5d2] py-6" key={faq.question}>
                <summary className="flex cursor-pointer list-none items-center justify-between gap-5 font-semibold marker:content-none">
                  <span className="flex items-start gap-4">
                    <span className="mt-0.5 hidden font-mono text-xs text-[#827889] sm:inline">
                      0{index + 1}
                    </span>
                    {faq.question}
                  </span>
                  <span
                    aria-hidden="true"
                    className="flex size-8 shrink-0 items-center justify-center rounded-full border border-[#bcb2c0] text-lg font-normal transition-transform group-open:rotate-45"
                  >
                    +
                  </span>
                </summary>
                <p className="max-w-2xl pt-4 text-sm leading-6 text-[#615b69] sm:pl-10">
                  {faq.answer}
                </p>
              </details>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="join-title" className="relative overflow-hidden bg-[#05051f] px-5 py-20 text-white sm:px-8 sm:py-24 lg:px-10 lg:py-28">
        <div aria-hidden="true" className="absolute inset-0 bg-[radial-gradient(circle_at_50%_120%,rgba(112,46,231,0.48),transparent_52%)]" />
        <div className="relative mx-auto max-w-4xl text-center">
          <p className="text-sm font-bold uppercase tracking-[0.18em] text-[#cdb8ff]">
            Your next step
          </p>
          <h2
            className="mt-4 text-4xl font-semibold leading-[1.06] tracking-[-0.045em] text-balance sm:text-5xl lg:text-6xl"
            id="join-title"
          >
            Take a closer look at life inside Aqua.
          </h2>
          <p className="mx-auto mt-6 max-w-2xl text-base leading-7 text-[#d7d5e4] sm:text-lg">
            Create an account to begin your journey, or return to your dashboard if
            you are already a member.
          </p>
          <LandingAccountActions />
        </div>
      </section>
    </main>

    <footer className="bg-[#020218] px-5 py-12 text-[#d7d5e4] sm:px-8 lg:px-10">
      <div className="mx-auto grid max-w-7xl gap-10 sm:grid-cols-[1fr_auto] sm:items-end">
        <div>
          <Link className="inline-flex items-center gap-4 text-white" href="/">
            <Image
              alt=""
              aria-hidden="true"
              className="size-14 rounded-xl object-cover"
              height={56}
              src="/aqua-lifestyle-logo.jpg"
              width={56}
            />
            <span>
              <span className="block text-base font-semibold">Aqua Lifestyle Club</span>
              <span className="mt-1 block text-xs text-white/60">Always connected</span>
            </span>
          </Link>
          <p className="mt-6 max-w-md text-sm leading-6 text-white/55">
            Membership, products and community in one connected experience.
          </p>
        </div>
        <nav aria-label="Footer navigation" className="flex flex-wrap gap-x-6 gap-y-3 text-sm">
          <Link className="transition hover:text-white" href="/#value">Why Aqua</Link>
          <Link className="transition hover:text-white" href="/#programmes">Programmes</Link>
          <Link className="transition hover:text-white" href="/catalog">Catalog</Link>
          <Link className="transition hover:text-white" href="/contact">Contact</Link>
        </nav>
      </div>
    </footer>
  </>
);
