import type { LucideIcon } from "lucide-react";
import {
  ArrowRight, BadgeCheck, CircleCheck, HeartPulse, Leaf,
  Network, ShieldCheck, Sparkles, Users,
} from "lucide-react";
import Link from "next/link";
import Image from "next/image";

import { LinkButton } from "@/src/shared/ui";
import { LandingAccountActions } from "./landing-account-actions";

const pillars = [
  { description: "Discover aQuathz products and how access can connect to membership.", icon: HeartPulse, title: "Health" },
  { description: "Keep products, programme information and activity in one experience.", icon: Sparkles, title: "Lifestyle" },
  { description: "Stay connected through the Area Leader and Facilitator network.", icon: Users, title: "Community" },
  { description: "Explore participation and leadership pathways in a local network.", icon: Network, title: "Opportunity" },
];

const pathways = [
  { accent: "bg-[#f7e3ca] text-[#111044]", description: "The starting point for club access, eligible products and ongoing member activity.", eyebrow: "Membership", icon: BadgeCheck, title: "A connected member experience" },
  { accent: "bg-[#c9f4d8] text-[#07552f]", description: "A distinct pathway centred on participation and monthly commitments.", eyebrow: "AQGreen", icon: Leaf, title: "Structured participation" },
  { accent: "bg-[#d9ccff] text-[#351077]", description: "A pathway connecting network participation with Facilitator and Area Leader roles.", eyebrow: "Onyx", icon: Network, title: "Community opportunity" },
];

const journey = [
  { description: "Learn about the club, pathways and products before creating an account.", title: "Explore" },
  { description: "Create an account to access information and actions relevant to you.", title: "Join" },
  { description: "Follow products, orders, programmes and club activity from your dashboard.", title: "Participate" },
];

const faqs = [
  { answer: "Aqua Lifestyle Club is a membership platform bringing together aQuathz products, member programmes and an area-based community network.", question: "What is Aqua Lifestyle Club?" },
  { answer: "No. You can learn about the club and browse the public product catalog before deciding to create an account.", question: "Do I need an account to browse products?" },
  { answer: "Product visibility and eligibility can depend on membership. Your account shows the information and actions available to you.", question: "Does every member see the same products?" },
  { answer: "Signed-in members use their dashboard to view the products, orders, programmes and club activity available for their role.", question: "Where do I manage my membership activity?" },
];

const SectionHeading = ({ description, eyebrow, id, light = false, title }: {
  description?: string; eyebrow: string; id: string; light?: boolean; title: string;
}) => (
  <div className="max-w-3xl">
    <p className={`text-xs font-bold uppercase tracking-[0.24em] ${light ? "text-[#cdb8ff]" : "text-[#5921b6]"}`}>{eyebrow}</p>
    <h2 className={`mt-4 text-3xl font-semibold leading-[1.05] tracking-[-0.045em] text-balance sm:text-5xl ${light ? "text-white" : "text-[#17111c]"}`} id={id}>{title}</h2>
    {description ? <p className={`mt-5 max-w-2xl text-base leading-7 sm:text-lg ${light ? "text-white/60" : "text-[#655d68]"}`}>{description}</p> : null}
  </div>
);

const Pillar = ({ description, icon: Icon, index, title }: { description: string; icon: LucideIcon; index: number; title: string }) => (
  <article className="group flex min-h-72 flex-col justify-between border-t border-[#d8d0c6] py-7 transition-colors hover:border-[#6424d0]">
    <div className="flex items-center justify-between">
      <span className="font-mono text-xs text-[#837986]">0{index + 1}</span>
      <Icon aria-hidden="true" className="size-6 text-[#6424d0] transition-transform duration-300 group-hover:-translate-y-1" strokeWidth={1.6} />
    </div>
    <div><h3 className="text-2xl font-semibold tracking-[-0.03em]">{title}</h3><p className="mt-3 max-w-xs text-sm leading-6 text-[#655d68]">{description}</p></div>
  </article>
);

const ProductVisual = () => (
  <div aria-hidden="true" className="relative mx-auto flex h-80 w-full max-w-lg items-end justify-center sm:h-[28rem]">
    <div className="absolute left-4 top-3 size-48 rounded-full bg-[#7130ee]/25 blur-3xl" />
    {[
      "h-[78%] w-[31%] from-[#7545df] via-[#34206f] to-[#120f1b]",
      "-ml-3 h-[54%] w-[24%] from-[#d9ccff] via-[#6f35cf] to-[#181123]",
      "-ml-2 h-[42%] w-[18%] from-[#afefc5] via-[#178d57] to-[#102219]",
    ].map((classes, index) => (
      <div className={`relative rounded-[2.5rem_2.5rem_1.4rem_1.4rem] border border-white/20 bg-gradient-to-b shadow-[0_35px_80px_rgba(0,0,0,0.38)] ${classes}`} key={classes}>
        <div className="absolute -top-5 left-1/2 h-7 w-10 -translate-x-1/2 rounded-t-lg bg-[#292033]" />
        {index === 0 ? <div className="absolute inset-x-2 top-[42%] border-y border-white/15 bg-black/25 py-4 text-center text-[9px] font-bold uppercase tracking-[0.2em] text-white/75">aQuathz</div> : null}
      </div>
    ))}
  </div>
);

export const LandingPage = () => (
  <>
    <main className="overflow-hidden bg-[#fbf5ec] text-[#17111c]">
      <section aria-labelledby="landing-title" className="relative isolate min-h-[calc(100svh-4rem)] overflow-hidden bg-[#05051f] text-white">
        <div aria-hidden="true" className="absolute inset-0 bg-[radial-gradient(circle_at_78%_25%,rgba(106,43,220,0.34),transparent_30%),radial-gradient(circle_at_15%_85%,rgba(23,135,82,0.16),transparent_28%)]" />
        <div aria-hidden="true" className="absolute -right-24 top-[15%] h-80 w-[52rem] -rotate-12 rounded-[100%] bg-gradient-to-r from-[#2b0b65] via-[#6120d2] to-[#8d52ff] opacity-80" />
        <div className="relative mx-auto grid min-h-[calc(100svh-4rem)] max-w-7xl items-center gap-10 px-4 py-16 sm:px-6 lg:grid-cols-[1.08fr_0.92fr] lg:px-8">
          <div className="relative z-10 max-w-3xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1.5 text-xs font-semibold text-white/75 backdrop-blur-sm"><ShieldCheck aria-hidden="true" className="size-4 text-[#cdb8ff]" />Membership &nbsp; Products &nbsp; Community</div>
            <h1 className="mt-8 text-5xl font-semibold leading-[0.93] tracking-[-0.065em] text-balance sm:text-7xl lg:text-[5.6rem]" id="landing-title">Live in health.<br /><span className="text-[#cdb8ff]">Inspire to wealth.</span></h1>
            <p className="mt-7 max-w-xl text-lg leading-8 text-white/65 sm:text-xl">A connected club experience bringing together aQuathz products, membership pathways and area-based community support.</p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <LinkButton className="rounded-full bg-[#7540e8] px-7 text-white shadow-none hover:bg-[#8655ef]" href="#value" size="lg">Discover Aqua<ArrowRight aria-hidden="true" className="size-4" /></LinkButton>
              <LinkButton className="rounded-full border-white/20 bg-white/5 px-7 text-white hover:bg-white/10" href="/catalog" size="lg" variant="outline">Browse products</LinkButton>
            </div>
            <p className="mt-7 flex items-center gap-2 text-sm text-white/55"><CircleCheck aria-hidden="true" className="size-4 text-[#a9eec1]" />Explore before you decide to join.</p>
          </div>
          <div className="relative flex min-h-80 items-center justify-center lg:min-h-[36rem]">
            <div className="relative z-10 grid aspect-[4/5] w-64 place-items-center overflow-hidden rounded-[8rem] border border-white/15 bg-[#111044]/45 p-7 shadow-2xl backdrop-blur-sm sm:w-80 sm:p-9"><div className="absolute inset-5 rounded-[7rem] border border-white/10" /><Image alt="Aqua Lifestyle Club" className="relative size-full rounded-full object-cover shadow-[0_24px_70px_rgba(0,0,0,0.45)]" height={640} priority sizes="(min-width: 640px) 248px, 200px" src="/aqua-lifestyle-logo.jpg" width={640} /></div>
            <div className="absolute bottom-6 right-0 z-20 max-w-56 rounded-2xl border border-white/15 bg-[#f7e3ca] p-5 text-[#111044] shadow-2xl sm:right-8"><p className="text-[10px] font-bold uppercase tracking-[0.22em] text-[#6b4f2f]">Always connected</p><p className="mt-2 text-sm font-semibold leading-5">Health, lifestyle, community and opportunity.</p></div>
          </div>
        </div>
      </section>

      <section aria-labelledby="problem-title" className="border-b border-[#ded6cc] px-4 py-20 sm:px-6 sm:py-28 lg:px-8">
        <div className="mx-auto grid max-w-7xl gap-10 lg:grid-cols-[0.75fr_1.25fr] lg:items-end">
          <p className="text-xs font-bold uppercase tracking-[0.24em] text-[#5921b6]">The everyday challenge</p>
          <div><h2 className="max-w-4xl text-4xl font-semibold leading-[1.04] tracking-[-0.05em] text-balance sm:text-6xl" id="problem-title">Products, participation and support should not feel disconnected.</h2><p className="mt-6 max-w-2xl text-lg leading-8 text-[#655d68]">Aqua brings the practical parts of club membership into one place, while keeping every pathway distinct.</p></div>
        </div>
      </section>

      <section aria-labelledby="value-title" className="scroll-mt-20 px-4 py-20 sm:px-6 sm:py-28 lg:px-8" id="value">
        <div className="mx-auto max-w-7xl"><SectionHeading description="A membership experience designed around the things that matter to everyday club life." eyebrow="Why Aqua" id="value-title" title="One club. Four connected ideas." /><div className="mt-14 grid gap-x-8 sm:grid-cols-2 lg:grid-cols-4">{pillars.map((pillar, index) => <Pillar index={index} key={pillar.title} {...pillar} />)}</div></div>
      </section>

      <section aria-labelledby="pathways-title" className="scroll-mt-20 bg-[#080722] px-4 py-20 text-white sm:px-6 sm:py-28 lg:px-8" id="programmes">
        <div className="mx-auto max-w-7xl"><SectionHeading description="Explore each pathway first. Your account shows the access and actions available to you." eyebrow="Membership overview" id="pathways-title" light title="Different pathways. One connected experience." />
          <div className="mt-14 grid gap-4 lg:grid-cols-3">{pathways.map((pathway, index) => <article className="group relative min-h-96 overflow-hidden rounded-3xl border border-white/10 bg-[#121030] p-7 sm:p-9" key={pathway.eyebrow}><div className={`inline-flex size-12 items-center justify-center rounded-2xl ${pathway.accent}`}><pathway.icon aria-hidden="true" className="size-6" strokeWidth={1.6} /></div><p className="mt-20 text-xs font-bold uppercase tracking-[0.22em] text-white/45">{pathway.eyebrow}</p><h3 className="mt-3 max-w-xs text-2xl font-semibold tracking-[-0.03em]">{pathway.title}</h3><p className="mt-4 max-w-sm text-sm leading-6 text-white/55">{pathway.description}</p><span aria-hidden="true" className="absolute -bottom-8 right-4 font-mono text-8xl font-bold text-white/[0.035]">0{index + 1}</span></article>)}</div>
        </div>
      </section>

      <section aria-labelledby="products-title" className="bg-[#0b0926] px-4 pb-20 text-white sm:px-6 sm:pb-28 lg:px-8">
        <div className="mx-auto grid max-w-7xl overflow-hidden rounded-b-[2.5rem] bg-[#21104d] lg:grid-cols-2">
          <div className="flex items-center p-8 sm:p-12 lg:p-16"><div><p className="text-xs font-bold uppercase tracking-[0.24em] text-[#d6aa45]">Featured products</p><h2 className="mt-4 text-4xl font-semibold leading-[1.05] tracking-[-0.045em] sm:text-5xl" id="products-title">See the product world behind the club.</h2><p className="mt-5 max-w-xl leading-7 text-white/60">Explore aQuathz water products, Spraythz and health sets. Availability and eligibility can depend on membership.</p><LinkButton className="mt-8 rounded-full border-white/15 bg-[#f7e3ca] px-7 text-[#111044] hover:bg-white" href="/catalog" size="lg" variant="outline">Explore the catalog<ArrowRight aria-hidden="true" className="size-4" /></LinkButton></div></div>
          <div className="flex min-h-96 items-center bg-[radial-gradient(circle_at_center,rgba(116,64,232,0.22),transparent_64%)] p-6 sm:p-10"><ProductVisual /></div>
        </div>
      </section>

      <section aria-labelledby="journey-title" className="scroll-mt-20 px-4 py-20 sm:px-6 sm:py-28 lg:px-8" id="how-it-works">
        <div className="mx-auto max-w-7xl"><SectionHeading description="Start with clear information, then move into an experience shaped around your access." eyebrow="How it works" id="journey-title" title="A considered path into the club." /><ol className="mt-14 border-y border-[#d8d0c6]">{journey.map((step, index) => <li className="grid gap-4 border-b border-[#d8d0c6] py-7 last:border-b-0 sm:grid-cols-[5rem_0.6fr_1fr] sm:items-center" key={step.title}><span className="font-mono text-sm text-[#5921b6]">0{index + 1}</span><h3 className="text-2xl font-semibold">{step.title}</h3><p className="max-w-xl text-sm leading-6 text-[#655d68]">{step.description}</p></li>)}</ol></div>
      </section>

      <section aria-labelledby="community-title" className="bg-[#e6f6e9] px-4 py-20 sm:px-6 sm:py-28 lg:px-8">
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[0.9fr_1.1fr] lg:items-center">
          <div className="relative min-h-80 overflow-hidden rounded-[2.5rem] bg-[#07552f] p-8 text-white sm:min-h-[30rem] sm:p-12"><div aria-hidden="true" className="absolute -bottom-28 -right-24 size-80 rounded-full border-[55px] border-[#a9eec1]/25" /><Users aria-hidden="true" className="relative size-16 text-[#c9f4d8]" strokeWidth={1.25} /><p className="relative mt-28 max-w-sm text-3xl font-semibold leading-tight tracking-[-0.04em] sm:mt-40 sm:text-4xl">People are the heart of the experience.</p></div>
          <div><SectionHeading description="Aqua's area-based model connects members with Area Leaders and Facilitators, bringing local support into the wider club experience." eyebrow="Community" id="community-title" title="Belong locally. Stay connected to the whole." /><div className="mt-8 flex items-start gap-4 border-t border-[#07552f]/20 pt-6"><ShieldCheck aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-[#07552f]" /><p className="max-w-xl text-sm leading-6 text-[#315b43]">Programme participation, roles and available actions remain visible through the member dashboard.</p></div></div>
        </div>
      </section>

      <section aria-labelledby="faq-title" className="scroll-mt-20 px-4 py-20 sm:px-6 sm:py-28 lg:px-8" id="faq">
        <div className="mx-auto grid max-w-7xl gap-12 lg:grid-cols-[0.75fr_1.25fr]"><SectionHeading description="Clear answers about the public and member experience." eyebrow="FAQ" id="faq-title" title="Questions before joining." /><div className="border-t border-[#d8d0c6]">{faqs.map((faq, index) => <details className="group border-b border-[#d8d0c6] py-6" key={faq.question}><summary className="flex cursor-pointer list-none items-center justify-between gap-6 font-semibold marker:content-none"><span className="flex items-center gap-4"><span className="hidden font-mono text-xs text-[#837986] sm:inline">0{index + 1}</span>{faq.question}</span><span aria-hidden="true" className="flex size-8 shrink-0 items-center justify-center rounded-full border border-[#bcb2c0] text-lg font-normal transition-transform group-open:rotate-45">+</span></summary><p className="max-w-2xl pt-4 text-sm leading-6 text-[#655d68] sm:pl-10">{faq.answer}</p></details>)}</div></div>
      </section>

      <section aria-labelledby="join-title" className="relative overflow-hidden bg-[#05051f] px-4 py-20 text-white sm:px-6 sm:py-28 lg:px-8"><div aria-hidden="true" className="absolute inset-0 bg-[radial-gradient(circle_at_50%_120%,rgba(112,46,231,0.55),transparent_48%)]" /><div className="relative mx-auto max-w-4xl text-center"><p className="text-xs font-bold uppercase tracking-[0.24em] text-[#cdb8ff]">Your next step</p><h2 className="mt-4 text-4xl font-semibold leading-[1.02] tracking-[-0.05em] text-balance sm:text-6xl" id="join-title">Take a closer look at life inside Aqua.</h2><p className="mx-auto mt-6 max-w-2xl text-lg leading-8 text-white/60">Create an account to begin your journey, or return to your dashboard if you are already a member.</p><LandingAccountActions /></div></section>
    </main>

    <footer className="bg-[#020218] px-4 py-10 text-white/55 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-8 sm:flex-row sm:items-center sm:justify-between"><Link className="inline-flex items-center gap-3 text-white" href="/"><Image alt="" aria-hidden="true" className="size-10 rounded-full object-cover" height={40} src="/aqua-lifestyle-logo.jpg" width={40} /><span className="font-semibold">Aqua Lifestyle Club</span></Link><nav aria-label="Footer navigation" className="flex flex-wrap gap-x-6 gap-y-3 text-sm"><Link className="hover:text-white" href="/#value">Why Aqua</Link><Link className="hover:text-white" href="/#programmes">Programmes</Link><Link className="hover:text-white" href="/catalog">Catalog</Link><Link className="hover:text-white" href="/contact">Contact</Link></nav></div><p className="mx-auto mt-8 max-w-7xl border-t border-white/10 pt-6 text-xs">Aqua Lifestyle Club. Membership, products and community in one connected experience.</p></footer>
  </>
);
