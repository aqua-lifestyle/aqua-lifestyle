"use client";

import { Droplets, Users, Wallet, Package, Calendar } from "lucide-react";

import { useAuthState } from "@/src/providers";
import { Card, LinkButton } from "@/src/shared/ui";

const features = [
  {
    icon: Users,
    title: "Membership management",
    description:
      "Join and manage your membership tier with transparent obligations and benefits.",
  },
  {
    icon: Wallet,
    title: "Savings account",
    description:
      "Track savings windows and balances tied to your membership tier.",
  },
  {
    icon: Package,
    title: "Products for you",
    description:
      "Browse products curated for your membership and purchase history.",
  },
  {
    icon: Calendar,
    title: "Area leadership",
    description:
      "Stay connected with area leaders and facilitators in your community.",
  },
];

export const LandingPage = () => {
  const { session } = useAuthState();
  const isAuthenticated = Boolean(session?.user);

  return (
    <main className="min-h-dvh bg-muted/30 text-foreground">
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-primary to-primary-dark opacity-95" />
        <div className="absolute inset-0 bg-[url('/aqua-pattern.svg')] bg-cover opacity-10" />
        <div className="relative mx-auto flex min-h-[70vh] max-w-7xl flex-col items-center justify-center px-4 py-20 text-center text-white sm:px-6 lg:px-8">
          <div className="flex size-16 items-center justify-center rounded-2xl bg-white/10 backdrop-blur">
            <Droplets className="size-8 text-white" />
          </div>
          <h1 className="mt-8 text-4xl font-bold tracking-tight sm:text-5xl lg:text-6xl">
            Aqua Lifestyle Club
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-white/80 sm:text-xl">
            Enterprise-grade club management, designed for modern teams and
            communities.
          </p>
          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            {isAuthenticated ? (
              <LinkButton href="/member" size="lg" variant="primary">
                Go to member area
              </LinkButton>
            ) : (
              <>
                <LinkButton href="/signup" size="lg" variant="primary">
                  Create account
                </LinkButton>
                <LinkButton
                  href="/login"
                  size="lg"
                  className="border-white/30 bg-white/10 text-white hover:bg-white/20"
                  variant="outline"
                >
                  Sign in
                </LinkButton>
              </>
            )}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-4 py-16 sm:px-6 lg:px-8">
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <Card key={feature.title} className="h-full">
              <div className="flex items-center gap-3">
                <div className="rounded-full bg-accent/10 p-3 text-accent">
                  <feature.icon className="size-6" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-foreground">
                    {feature.title}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {feature.description}
                  </p>
                </div>
              </div>
            </Card>
          ))}
        </div>
      </section>
    </main>
  );
};
