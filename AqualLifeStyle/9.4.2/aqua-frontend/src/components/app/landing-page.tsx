import {
  ArrowDownRight,
  BadgeCheck,
  Leaf,
  Network,
  PackageSearch,
  ShieldCheck,
  Users,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";

import { LandingAccountActions } from "./landing-account-actions";
import {
  LandingEyebrow,
  LandingLinkButton,
  landingContainerClassName,
} from "./landing-primitives";

const discoveryMoments = [
  {
    body: "Learn what Aqua offers and browse public information without creating an account.",
    label: "Understand",
    title: "Start with the club.",
  },
  {
    body: "Browse wellbeing products, see how membership shapes access, and compare AQGreen and Onyx as distinct programmes.",
    label: "Explore",
    title: "See what Aqua offers.",
  },
  {
    body: "Continue to the public catalog or sign in for information and actions tied to your account.",
    label: "Continue",
    title: "Choose your next step.",
  },
];

const participationOptions = [
  {
    body: "Membership status helps determine eligible products and the account actions available to a customer.",
    icon: BadgeCheck,
    label: "Club access",
    title: "Membership",
  },
  {
    body: "AQGreen is a distinct programme with its own participation record and monthly commitments.",
    icon: Leaf,
    label: "Programme",
    title: "AQGreen",
  },
  {
    body: "Onyx is a separate programme with its own participation and network placement records.",
    icon: Network,
    label: "Programme",
    title: "Onyx",
  },
];

const journey = [
  {
    body: "Review the club and browse available products.",
    title: "Explore",
  },
  {
    body: "Where self-registration is available, create an account.",
    title: "Create",
  },
  {
    body: "Sign in to see information and actions available to you.",
    title: "Continue",
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

const ChapterMarker = ({
  children,
  index,
  light = false,
}: {
  children: string;
  index: string;
  light?: boolean;
}) => (
  <div className={`flex items-center gap-3 text-xs font-bold uppercase tracking-[0.16em] ${light ? "text-aqua-lavender-strong" : "text-aqua-violet-dark"}`}>
    <span className={`font-mono ${light ? "text-white/65" : "text-aqua-muted"}`}>{index}</span>
    <span className={`h-px w-8 ${light ? "bg-white/20" : "bg-aqua-line"}`} />
    {children}
  </div>
);

export const LandingPage = () => (
  <>
    <main
      className="scroll-mt-16 overflow-x-clip bg-aqua-canvas text-aqua-ink focus:outline-none"
      id="main-content"
      tabIndex={-1}
    >
      <section
        aria-labelledby="landing-title"
        className="relative min-h-[calc(100svh-4rem)] overflow-hidden bg-aqua-canvas"
      >
        <div aria-hidden="true" className="absolute inset-y-0 left-[11%] hidden w-px bg-aqua-line/65 lg:block" />
        <div aria-hidden="true" className="absolute inset-y-0 right-[11%] hidden w-px bg-aqua-line/65 lg:block" />
        <div className={`${landingContainerClassName} flex min-h-[calc(100svh-4rem)] flex-col py-8 sm:py-12 lg:py-16 [@media(max-height:600px)]:py-5`}>
          <div className="flex items-center justify-between">
            <ChapterMarker index="00">Aqua Lifestyle Club</ChapterMarker>
            <p className="hidden text-xs font-semibold uppercase tracking-[0.14em] text-aqua-muted sm:block">
              Always connected
            </p>
          </div>

          <div className="my-auto py-12 sm:py-16 lg:py-20 [@media(max-height:600px)]:py-4">
            <h1
              className="font-aqua-display text-5xl font-normal leading-[0.92] tracking-[-0.045em] sm:text-[clamp(4rem,8.6vw,8.5rem)] sm:leading-[0.86] [@media(max-height:600px)]:text-[3.25rem]"
              id="landing-title"
            >
              <span className="block">Live in health.</span>
              <span className="mt-4 block text-right text-aqua-violet sm:mt-6">
                Inspire to wealth.
              </span>
            </h1>
          </div>

          <div className="grid gap-6 border-t border-aqua-line pt-6 sm:grid-cols-12 sm:items-center sm:gap-8 sm:pt-8 lg:gap-8 xl:gap-10">
            <div className="sm:col-span-7 lg:col-span-4 xl:col-span-5 md:[@media(max-height:600px)]:col-span-5">
              <p className="max-w-lg text-lg leading-8 text-aqua-muted md:[@media(max-height:600px)]:text-sm md:[@media(max-height:600px)]:leading-6">
                Aqua Lifestyle Club combines aQuathz wellbeing products, membership,
                and participation in the distinct AQGreen and Onyx programmes.
              </p>
            </div>
            <div className="flex items-center gap-4 sm:col-span-5 sm:justify-end lg:col-span-4 lg:justify-center xl:col-span-3 md:[@media(max-height:600px)]:hidden">
              <Image
                alt="Aqua Lifestyle Club"
                className="size-28 rounded-aqua-control object-cover shadow-sm sm:size-36"
                height={144}
                priority
                src="/aqua-lifestyle-logo.jpg"
                width={144}
              />
              <p className="max-w-28 text-xs font-semibold uppercase leading-5 tracking-[0.12em] text-aqua-muted">
                Products. Membership. Programmes.
              </p>
            </div>
            <div className="flex flex-col gap-3 sm:col-span-12 sm:flex-row sm:justify-end lg:col-span-4 lg:flex-col lg:items-stretch xl:flex-row xl:items-center md:[@media(max-height:600px)]:col-span-7 md:[@media(max-height:600px)]:flex-row">
              <LandingLinkButton href="#welcome">
                See what Aqua offers
                <ArrowDownRight aria-hidden="true" className="size-4" />
              </LandingLinkButton>
              <LandingLinkButton href="/catalog" tone="secondary-light">
                Browse products
              </LandingLinkButton>
            </div>
          </div>
        </div>
      </section>

      <section aria-labelledby="trust-title" className="border-y border-aqua-line bg-aqua-surface">
        <h2 className="sr-only" id="trust-title">What Aqua offers</h2>
        <div className={`${landingContainerClassName} grid md:grid-cols-3`}>
          {[
            ["Products", "Browse water products, Spraythz and health sets in the public catalog."],
            ["Membership", "Membership helps determine product eligibility and account access."],
            ["Programmes", "AQGreen and Onyx are separate participation programmes with different paths."],
          ].map(([title, body]) => (
            <div
              className="border-b border-aqua-line py-8 transition-colors last:border-b-0 hover:bg-aqua-canvas/60 md:border-b-0 md:border-r md:px-8 md:py-10 md:first:pl-0 md:last:border-r-0 md:last:pr-0"
              key={title}
            >
              <h3 className="text-2xl font-semibold tracking-[-0.03em]">{title}.</h3>
              <p className="mt-3 max-w-sm text-sm leading-6 text-aqua-muted">{body}</p>
            </div>
          ))}
        </div>
      </section>

      <section aria-labelledby="welcome-title" className="relative scroll-mt-16 overflow-hidden bg-aqua-cream" id="welcome">
        <svg
          aria-hidden="true"
          className="absolute inset-x-0 bottom-0 h-44 w-full text-aqua-violet/10 sm:h-56"
          preserveAspectRatio="none"
          viewBox="0 0 1440 240"
        >
          <path d="M-80 120 Q 280 20 640 120 T 1360 120 T 2080 120" fill="none" stroke="currentColor" strokeWidth="2" />
          <path d="M-120 170 Q 240 70 600 170 T 1320 170 T 2040 170" fill="none" stroke="currentColor" strokeWidth="1" />
          <path d="M-40 220 Q 320 120 680 220 T 1400 220 T 2120 220" fill="none" stroke="currentColor" strokeWidth="1" />
        </svg>
        <div className={`${landingContainerClassName} relative grid gap-10 py-16 sm:gap-12 sm:py-20 md:grid-cols-12 md:items-end lg:gap-16 lg:py-32`}>
          <div className="md:col-span-8">
            <ChapterMarker index="01">What Aqua is</ChapterMarker>
            <h2
              className="font-aqua-display mt-10 max-w-5xl text-5xl font-normal leading-[0.98] tracking-[-0.04em] text-balance sm:text-6xl lg:text-7xl"
              id="welcome-title"
            >
              A lifestyle club built around practical wellbeing.
            </h2>
          </div>
          <div className="md:col-span-4 lg:col-span-3 lg:col-start-10">
            <p className="text-lg leading-8 text-aqua-muted">
              Explore products, understand membership and learn how AQGreen and Onyx
              participation works before choosing your next step.
            </p>
            <div className="mt-8 h-px w-full bg-aqua-ink/15" />
          </div>
        </div>
      </section>

      <section aria-labelledby="problem-title" className="scroll-mt-16 bg-aqua-navy text-white" id="story">
        <div className={`${landingContainerClassName} relative py-16 sm:py-20 lg:py-32`}>
          <span aria-hidden="true" className="absolute right-8 top-8 font-mono text-[11rem] leading-none text-white/[0.025] sm:text-[18rem]">
            02
          </span>
          <ChapterMarker index="02" light>Why Aqua</ChapterMarker>
          <div className="relative mt-12 grid gap-10 sm:mt-16 md:grid-cols-12 md:items-end lg:gap-12">
            <h2
              className="text-5xl font-semibold leading-[0.98] tracking-[-0.05em] text-balance sm:text-6xl md:col-span-8 lg:text-7xl"
              id="problem-title"
            >
              A club should make every path clear.
            </h2>
            <p className="max-w-md text-base leading-7 text-aqua-dark-muted md:col-span-4">
              Browse the public catalog first. Learn how membership affects access.
              Then explore AQGreen and Onyx as separate programmes when you are ready.
            </p>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="solution-title"
        className="scroll-mt-24 bg-aqua-canvas"
        id="solution"
      >
        <div className={`${landingContainerClassName} grid gap-12 py-16 sm:py-20 md:grid-cols-12 md:gap-8 lg:gap-16 lg:py-32`}>
          <div className="self-start md:col-span-4 lg:sticky lg:top-28 lg:col-span-5">
            <ChapterMarker index="03">The experience</ChapterMarker>
            <h2
              className="mt-8 text-4xl font-semibold leading-[1.02] tracking-[-0.045em] text-balance sm:text-5xl md:text-4xl lg:text-6xl"
              id="solution-title"
            >
              Understand Aqua before choosing a path.
            </h2>
            <p className="mt-6 max-w-md text-base leading-7 text-aqua-muted">
              The public experience introduces the club, its products and ways to
              participate before you need to sign in.
            </p>
          </div>

          <div className="md:col-span-7 md:col-start-6 lg:col-span-6 lg:col-start-7">
            {discoveryMoments.map((moment) => (
              <article
                className="flex flex-col gap-12 border-t border-aqua-line py-10 first:border-t-0 first:pt-0 sm:min-h-[18rem] sm:justify-between md:min-h-[20rem] lg:min-h-[24rem] lg:py-12"
                key={moment.title}
              >
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-aqua-violet-dark">
                  {moment.label}
                </p>
                <div>
                  <h3 className="text-4xl font-semibold leading-[1.04] tracking-[-0.04em] sm:text-5xl">
                    {moment.title}
                  </h3>
                  <p className="mt-6 max-w-lg text-base leading-7 text-aqua-muted">
                    {moment.body}
                  </p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section
        aria-labelledby="products-title"
        className="scroll-mt-24 border-y border-aqua-line bg-aqua-surface"
        id="products"
      >
        <div className={`${landingContainerClassName} py-16 sm:py-20 lg:py-32`}>
          <div className="grid gap-12 md:grid-cols-12 md:gap-8 lg:gap-12">
            <div className="md:col-span-4">
              <ChapterMarker index="04">Products</ChapterMarker>
              <h2
                className="mt-8 text-4xl font-semibold leading-[1.04] tracking-[-0.045em] sm:text-5xl"
                id="products-title"
              >
                Wellbeing, made tangible.
              </h2>
              <p className="mt-6 max-w-sm text-base leading-7 text-aqua-muted">
                Aqua&apos;s product world includes aQuathz water products, Spraythz and
                health sets. The public catalog lets curiosity come first.
              </p>
              <LandingLinkButton className="mt-8" href="/catalog">
                Open the catalog
                <ArrowDownRight aria-hidden="true" className="size-4" />
              </LandingLinkButton>
            </div>

            <div className="md:col-span-7 md:col-start-6">
              {["Water products", "Spraythz", "Health sets"].map((product) => (
                <Link
                  className="group flex min-h-28 items-center justify-between gap-6 border-b border-aqua-line px-4 py-6 transition-colors first:border-t hover:bg-aqua-canvas focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-aqua-violet sm:min-h-36 sm:py-7 lg:min-h-40"
                  href="/catalog"
                  key={product}
                >
                  <h3 className="text-3xl font-semibold tracking-[-0.035em] transition-transform duration-300 motion-safe:group-hover:translate-x-2 sm:text-4xl">
                    {product}
                  </h3>
                  <PackageSearch aria-hidden="true" className="size-5 shrink-0 text-aqua-violet transition-transform duration-200 motion-safe:group-hover:translate-x-1" strokeWidth={1.75} />
                </Link>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="participation-title"
        className="scroll-mt-24 bg-aqua-navy text-white"
        id="programmes"
      >
        <div className={`${landingContainerClassName} pt-16 sm:pt-20 lg:pt-32`}>
          <ChapterMarker index="05" light>Participation</ChapterMarker>
          <div className="mt-8 grid gap-8 pb-12 sm:pb-16 md:grid-cols-12 md:items-end lg:pb-20">
            <h2
              className="text-4xl font-semibold leading-[1.04] tracking-[-0.045em] sm:text-5xl md:col-span-7 lg:text-6xl"
              id="participation-title"
            >
              Different ways to take part. Clearly held.
            </h2>
            <p className="max-w-md text-base leading-7 text-aqua-dark-muted md:col-span-4 md:col-start-9">
              Membership shapes access, while AQGreen and Onyx remain distinct
              programmes with separate participation records.
            </p>
          </div>
        </div>

        <div className="border-t border-white/10">
          <div className={`${landingContainerClassName} grid md:grid-cols-3`}>
            {participationOptions.map((option) => (
              <article
                className="group flex min-h-64 flex-col justify-between border-b border-white/10 py-10 transition-colors last:border-b-0 hover:bg-white/[0.025] sm:min-h-72 md:min-h-96 md:border-b-0 md:border-r md:px-6 md:last:border-r-0 lg:min-h-[28rem] lg:px-10 lg:first:pl-0 lg:last:pr-0"
                key={option.title}
              >
                <option.icon aria-hidden="true" className="size-8 text-aqua-gold transition-transform duration-200 motion-safe:group-hover:-translate-y-0.5" strokeWidth={1.5} />
                <div>
                  <p className="text-xs font-bold uppercase tracking-[0.16em] text-aqua-lavender-strong">
                    {option.label}
                  </p>
                  <h3 className="mt-4 text-4xl font-semibold tracking-[-0.04em]">{option.title}</h3>
                  <p className="mt-6 max-w-sm text-sm leading-6 text-aqua-dark-muted">
                    {option.body}
                  </p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="benefits-title" className="bg-aqua-cream">
        <div className={`${landingContainerClassName} py-16 sm:py-20 lg:py-32`}>
          <ChapterMarker index="06">Why clarity matters</ChapterMarker>
          <h2 className="sr-only" id="benefits-title">Benefits of a clearer experience</h2>
          <div className="mt-12 space-y-2 text-[clamp(2.3rem,6vw,6.5rem)] font-semibold leading-[0.98] tracking-[-0.055em] sm:mt-16">
            <p className="text-aqua-ink">Understand the offer.</p>
            <p className="text-center text-aqua-violet">Choose with confidence.</p>
            <p className="text-right text-aqua-ink">Continue when ready.</p>
          </div>
          <div className="mt-12 grid gap-8 border-t border-aqua-ink/15 pt-8 sm:mt-16 md:grid-cols-3">
            <p className="text-sm leading-6 text-aqua-muted">Browse the public catalog without creating an account.</p>
            <p className="text-sm leading-6 text-aqua-muted">See how membership, AQGreen and Onyx differ before choosing a path.</p>
            <p className="text-sm leading-6 text-aqua-muted">Sign in when you need information and actions specific to your account.</p>
          </div>
        </div>
      </section>

      <section
        aria-labelledby="journey-title"
        className="scroll-mt-24 bg-aqua-surface"
        id="how-it-works"
      >
        <div className={`${landingContainerClassName} py-16 sm:py-20 lg:py-32`}>
          <div className="grid gap-10 md:grid-cols-12 md:items-end lg:gap-12">
            <div className="md:col-span-5">
              <ChapterMarker index="07">The route</ChapterMarker>
              <h2
                className="mt-8 text-4xl font-semibold leading-[1.04] tracking-[-0.045em] sm:text-5xl"
                id="journey-title"
              >
                From public browsing to account access.
              </h2>
            </div>
            <p className="max-w-md text-base leading-7 text-aqua-muted md:col-span-4 md:col-start-9">
              Move from public discovery to account-specific information at a pace that
              matches your needs.
            </p>
          </div>

          <ol className="relative mt-16 grid gap-12 before:absolute before:left-4 before:top-4 before:h-[calc(100%-2rem)] before:w-px before:bg-aqua-line sm:mt-20 md:grid-cols-3 md:gap-0 md:before:left-0 md:before:right-0 md:before:top-4 md:before:h-px md:before:w-full">
            {journey.map((step, index) => (
              <li className="relative pl-12 md:px-8 md:pt-16 md:first:pl-0 md:last:pr-0" key={step.title}>
                <span className="absolute left-0 top-0 flex size-8 items-center justify-center rounded-full border border-aqua-violet bg-aqua-surface font-mono text-xs font-semibold text-aqua-violet-dark md:left-8 md:first:left-0">
                  {index + 1}
                </span>
                <h3 className="text-2xl font-semibold tracking-[-0.025em]">{step.title}</h3>
                <p className="mt-3 max-w-xs text-sm leading-6 text-aqua-muted">{step.body}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section aria-labelledby="community-title" className="scroll-mt-24 overflow-hidden bg-aqua-lavender" id="community">
        <div className={`${landingContainerClassName} relative py-16 sm:py-20 lg:py-32`}>
          <span aria-hidden="true" className="absolute -right-4 top-10 text-[10rem] font-semibold leading-none tracking-[-0.08em] text-aqua-violet/[0.055] sm:text-[17rem] lg:text-[24rem]">
            LOCAL
          </span>
          <div className="relative grid gap-12 md:grid-cols-12 md:items-end lg:gap-16">
            <div className="md:col-span-7">
              <ChapterMarker index="08">Community</ChapterMarker>
              <h2
                className="mt-8 text-5xl font-semibold leading-[0.98] tracking-[-0.05em] text-balance sm:text-6xl md:text-5xl lg:text-7xl"
                id="community-title"
              >
                A club organised through local Areas.
              </h2>
            </div>
            <div className="md:col-span-4 md:col-start-9">
              <Users aria-hidden="true" className="size-9 text-aqua-violet-dark" strokeWidth={1.5} />
              <p className="mt-8 text-base leading-7 text-aqua-muted">
                Aqua uses Areas to organise the customer experience, including whether
                self-registration is available. Area Leader and Facilitator roles are a
                separate part of Aqua&apos;s developing local model.
              </p>
              <div className="mt-8 flex items-start gap-3 border-t border-aqua-violet/20 pt-6">
                <ShieldCheck aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-aqua-violet-dark" />
                <p className="text-sm leading-6 text-aqua-muted">
                  Account access reflects Area settings, customer status and role.
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section aria-labelledby="faq-title" className="scroll-mt-16 bg-aqua-canvas" id="faq">
        <div className={`${landingContainerClassName} grid gap-12 py-16 sm:py-20 md:grid-cols-12 md:gap-8 lg:gap-16 lg:py-32`}>
          <div className="md:col-span-4">
            <ChapterMarker index="09">Questions</ChapterMarker>
            <h2
              className="mt-8 text-4xl font-semibold leading-[1.04] tracking-[-0.045em] sm:text-5xl"
              id="faq-title"
            >
              Pause here.
              <span className="block text-aqua-violet">Ask anything.</span>
            </h2>
            <p className="mt-6 max-w-sm text-base leading-7 text-aqua-muted">
              The essentials about public browsing, accounts and programme participation.
            </p>
          </div>

          <div className="border-t border-aqua-line md:col-span-7 md:col-start-6">
            {faqs.map((faq) => (
              <details className="group border-b border-aqua-line" key={faq.question}>
                <summary className="flex min-h-20 cursor-pointer list-none items-center justify-between gap-6 py-5 font-semibold transition-colors marker:content-none hover:text-aqua-violet-dark sm:min-h-24 sm:py-6">
                  <span>{faq.question}</span>
                  <span aria-hidden="true" className="text-2xl font-light text-aqua-violet transition-transform group-open:rotate-45">
                    +
                  </span>
                </summary>
                <p className="max-w-2xl pb-8 text-sm leading-6 text-aqua-muted">
                  {faq.answer}
                </p>
              </details>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="join-title" className="bg-aqua-navy text-white">
        <div className={`${landingContainerClassName} py-16 sm:py-20 lg:py-32`}>
          <div className="grid gap-10 md:grid-cols-12 md:items-end lg:gap-12">
            <div className="md:col-span-7 lg:col-span-8">
              <LandingEyebrow light>The next chapter</LandingEyebrow>
              <h2
                className="mt-8 text-5xl font-semibold leading-[0.96] tracking-[-0.05em] text-balance sm:text-6xl lg:text-7xl"
                id="join-title"
              >
                Start with curiosity.
                <span className="block text-aqua-lavender-strong">Continue with clarity.</span>
              </h2>
            </div>
            <div className="md:col-span-4 md:col-start-9">
              <p className="text-base leading-7 text-aqua-dark-muted">
                Explore the public catalog or enter member access to continue with the
                Area and account options available to you.
              </p>
              <LandingAccountActions />
            </div>
          </div>
        </div>
      </section>
    </main>

    <footer className="bg-aqua-navy text-aqua-dark-muted">
      <div className={`${landingContainerClassName} grid gap-10 border-t border-white/10 py-12 sm:grid-cols-[1fr_auto] sm:items-end`}>
        <Link className="inline-flex items-center gap-4 text-white" href="/">
          <Image
            alt=""
            aria-hidden="true"
            className="size-12 rounded-aqua-control object-cover"
            height={48}
            src="/aqua-lifestyle-logo.jpg"
            width={48}
          />
          <span>
            <span className="block text-sm font-semibold">Aqua Lifestyle Club</span>
            <span className="mt-1 block text-xs text-white/65">Always connected</span>
          </span>
        </Link>
        <nav aria-label="Footer navigation" className="flex flex-wrap gap-x-6 gap-y-3 text-sm">
          <Link className="transition-colors hover:text-white" href="/#welcome">About</Link>
          <Link className="transition-colors hover:text-white" href="/#products">Products</Link>
          <Link className="transition-colors hover:text-white" href="/#programmes">Programmes</Link>
          <Link className="transition-colors hover:text-white" href="/#community">Community</Link>
          <Link className="transition-colors hover:text-white" href="/contact">Contact</Link>
        </nav>
      </div>
    </footer>
  </>
);
