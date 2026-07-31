import { ArrowRight, CircleHelp, PackageSearch, UserRound } from "lucide-react";

import { Breadcrumb, Card, LinkButton } from "@/src/shared/ui";

const supportPaths = [
  {
    action: "Browse the catalog",
    description:
      "Explore the public product catalog before creating an account or signing in.",
    href: "/catalog",
    icon: PackageSearch,
    title: "Product questions",
  },
  {
    action: "Create an account",
    description:
      "Check registration availability for your Area and start your member journey.",
    href: "/signup",
    icon: UserRound,
    title: "New to Aqua",
  },
  {
    action: "Sign in",
    description:
      "Access the dashboard, enquiries and account tools available for your role.",
    href: "/login",
    icon: CircleHelp,
    title: "Existing member support",
  },
];

export const ContactPage = () => (
  <main className="min-h-dvh bg-muted/30 px-4 py-10 text-foreground sm:px-6 sm:py-14 lg:px-8">
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-10">
      <header>
        <Breadcrumb items={[{ href: "/", label: "Home" }, { label: "Help" }]} />
        <p className="mt-8 text-xs font-bold uppercase tracking-[0.2em] text-accent">
          Help and support
        </p>
        <h1 className="mt-3 max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">
          Find the right place to continue
        </h1>
        <p className="mt-5 max-w-2xl text-lg leading-8 text-muted-foreground">
          Choose the path that matches what you need. We only show contact options
          currently supported by the Aqua platform.
        </p>
      </header>

      <section aria-label="Support options" className="grid gap-5 lg:grid-cols-3">
        {supportPaths.map((path) => (
          <Card className="flex h-full flex-col items-start p-7" key={path.title}>
            <div className="flex size-11 items-center justify-center rounded-full bg-accent/10 text-accent">
              <path.icon aria-hidden="true" className="size-5" />
            </div>
            <h2 className="mt-6 text-xl font-bold">{path.title}</h2>
            <p className="mt-3 flex-1 text-sm leading-6 text-muted-foreground">
              {path.description}
            </p>
            <LinkButton className="mt-7" href={path.href} variant="outline">
              {path.action}
              <ArrowRight aria-hidden="true" className="size-4" />
            </LinkButton>
          </Card>
        ))}
      </section>

      <aside className="rounded-2xl border border-border bg-card p-6 sm:p-8">
        <h2 className="font-bold">Why there is no message form</h2>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-muted-foreground">
          Aqua does not currently provide a public contact-message service. We do not
          collect a message or claim it was delivered until that service is available.
        </p>
      </aside>
    </div>
  </main>
);
