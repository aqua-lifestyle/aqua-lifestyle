import { Badge, Card, LinkButton } from "@/src/shared/ui";

const demoSteps = [
  {
    description: "Confirm the backend catalog is reachable before creating records.",
    href: "/products",
    label: "Products",
    status: "Live read",
  },
  {
    description: "Review membership tiers, then use one during customer registration.",
    href: "/memberships",
    label: "Memberships",
    status: "Live read",
  },
  {
    description: "Create a customer with validated form input and optional membership.",
    href: "/customers/register",
    label: "Register customer",
    status: "Live write",
  },
  {
    description: "Create an enquiry for a customer and product, then manage its workflow.",
    href: "/enquiries",
    label: "Enquiries",
    status: "Workflow",
  },
] as const;

export default function Home() {
  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-5">
          <div className="flex flex-wrap gap-3">
            <Badge tone="success">ABP integration demo</Badge>
            <Badge>Lean validation path</Badge>
          </div>
          <div className="max-w-3xl">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="mt-2 text-4xl font-semibold tracking-tight">
              Frontend demo hub
            </h1>
            <p className="mt-4 text-base leading-7 text-zinc-600">
              Start here to validate the end-to-end flow in small, observable
              slices: catalog data, memberships, customer registration, and
              enquiry workflow actions.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/products" variant="primary">
              Start with products
            </LinkButton>
            <LinkButton href="/enquiries">Open enquiries</LinkButton>
          </div>
        </header>

        <section className="grid gap-4 lg:grid-cols-4">
          {demoSteps.map((step, index) => (
            <Card className="flex flex-col justify-between gap-6" key={step.href}>
              <div>
                <div className="flex items-start justify-between gap-4">
                  <p className="text-sm font-semibold text-emerald-700">
                    Step {index + 1}
                  </p>
                  <Badge tone="neutral">{step.status}</Badge>
                </div>
                <h2 className="mt-4 text-lg font-semibold">{step.label}</h2>
                <p className="mt-3 text-sm leading-6 text-zinc-600">
                  {step.description}
                </p>
              </div>
              <LinkButton href={step.href}>Open</LinkButton>
            </Card>
          ))}
        </section>
      </div>
    </main>
  );
}
