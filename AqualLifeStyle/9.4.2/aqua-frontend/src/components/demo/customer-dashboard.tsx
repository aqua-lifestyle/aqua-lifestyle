"use client";

import {
  Building2,
  Calendar,
  Mail,
  Package,
  ShieldCheck,
  User,
  Wallet,
} from "lucide-react";

import { useAuthState } from "@/src/providers";
import { Badge, Card, StatusMessage } from "@/src/shared/ui";

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "long",
    timeStyle: "short",
  }).format(new Date(date));
};

export const CustomerDashboard = () => {
  const { session } = useAuthState();
  const user = session?.user;

  const getInitials = () => {
    if (!user?.name) return "?";
    const parts = user.name.trim().split(/\s+/);
    const first = parts[0]?.[0] ?? "";
    const last = parts.length > 1 ? parts[parts.length - 1]?.[0] : "";
    return `${first}${last}`.toUpperCase();
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex size-16 items-center justify-center rounded-2xl bg-gradient-to-br from-accent to-accent-dark text-2xl font-bold text-white shadow-md">
              {getInitials()}
            </div>
            <div>
              <p className="text-sm font-semibold text-accent">Welcome back</p>
              <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
                {user?.name ?? "Member"}
              </h1>
              <p className="mt-1 text-base text-muted-foreground">
                {user?.email}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="size-4" />
            <span>{formatDate(new Date().toISOString())}</span>
            <Badge tone="accent" className="ml-2">
              Member
            </Badge>
          </div>
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <User className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Membership status</p>
              <p className="text-2xl font-bold">Active</p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Wallet className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Savings account</p>
              <p className="text-2xl font-bold">Coming soon</p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-info/10 p-3 text-info">
              <Package className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Products</p>
              <p className="text-2xl font-bold">Filtered for you</p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <ShieldCheck className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Account security</p>
              <p className="text-2xl font-bold">Verified</p>
            </div>
          </Card>
        </section>

        <StatusMessage tone="info">
          Your customer dashboard is being set up. You will soon be able to view
          your savings account, filtered products, and order history here.
        </StatusMessage>

        <section className="grid gap-6 lg:grid-cols-2">
          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Wallet className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">Savings account</h2>
            </div>
            <div className="mt-4 space-y-3 text-sm text-muted-foreground">
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Current balance</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Savings window</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Membership tier</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Package className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">Products for you</h2>
            </div>
            <div className="mt-4 space-y-3 text-sm text-muted-foreground">
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Featured products</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Recommended for you</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>New arrivals</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
            </div>
          </Card>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Mail className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">My enquiries</h2>
            </div>
            <div className="mt-4 text-sm text-muted-foreground">
              Your recent enquiries will appear here once available.
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Building2 className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">My area</h2>
            </div>
            <div className="mt-4 text-sm text-muted-foreground">
              Your area leader and facilitator information will appear here once
              available.
            </div>
          </Card>
        </section>
      </div>
    </main>
  );
};
