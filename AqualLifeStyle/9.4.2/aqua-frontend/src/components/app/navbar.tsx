"use client";

import {
  Building2,
  ChevronDown,
  Home,
  LayoutDashboard,
  Menu,
  MessageSquare,
  Network,
  Package,
  Plus,
  Users,
  UserPlus,
  DollarSign,
  X,
  User,
  Mail,
  PiggyBank,
} from "lucide-react";
import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { cn } from "@/src/shared/lib/utils";

import { useAuthState } from "@/src/providers";
import { isAreaLeader, isFacilitator, isSystemAdmin } from "@/src/shared/auth/roles";
import { TenantSwitcher } from "./tenant-switcher";
import { UserMenu } from "./user-menu";
import { landingContainerClassName } from "./landing-primitives";

const mainLinks = [
  { href: "/dashboard", icon: LayoutDashboard, label: "Dashboard", permission: null },
  { href: "/customers", icon: Users, label: "Customers", permission: "Pages.Customers" },
  { href: "/products", icon: Package, label: "Products", permission: "Pages.Products" },
  { href: "/enquiries", icon: MessageSquare, label: "Enquiries", permission: "Pages.Enquiries" },
];

const moreLinks = [
  { href: "/memberships", icon: Building2, label: "Memberships", permission: "Pages.Memberships" },
  { href: "/order-intents", icon: Home, label: "Order intents", permission: "Pages.Orders" },
  { href: "/facilitator", icon: UserPlus, label: "Facilitators", permission: "Pages.Facilitators" },
  { href: "/facilitator/dashboard", icon: LayoutDashboard, label: "Facilitator dashboard", permission: "Pages.Facilitators" },
  { href: "/facilitator/my-referrals", icon: DollarSign, label: "My referrals", permission: "Pages.Referrals" },
  { href: "/member", icon: User, label: "Club Member", permission: "Aqua.Orders.ViewSelf" },
  { href: "/member/programmes", icon: Network, label: "My programmes", permission: "Aqua.ProgrammeParticipations.ViewSelf" },
  { href: "/member/savings", icon: PiggyBank, label: "My savings", permission: "Aqua.Savings.ViewSelf" },
  { href: "/member/loans", icon: DollarSign, label: "My loans", permission: "Aqua.Loans.ViewSelf" },
  { href: "/member/entry-commitments", icon: DollarSign, label: "AQGreen commitments", permission: "Aqua.EntryMonthlyObligations.ViewSelf" },
  { href: "/member/enquiries", icon: MessageSquare, label: "My enquiries", permission: "Aqua.Enquiries.ViewSelf" },
  { href: "/catalog", icon: Package, label: "Catalog", permission: null },
  { href: "/contact", icon: Mail, label: "Contact", permission: null },
];

const publicLinks = [
  { href: "/#welcome", label: "Welcome" },
  { href: "/#products", label: "Wellbeing" },
  { href: "/#programmes", label: "Participation" },
  { href: "/#community", label: "Belong" },
  { href: "/#faq", label: "Questions" },
];

export const Navbar = () => {
  const pathname = usePathname();
  const isLanding = pathname === "/";
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const [isMoreOpen, setIsMoreOpen] = useState(false);
  const { isAuthenticated, session } = useAuthState();
  const areaLeaderLinks = isAreaLeader(session?.user?.role)
    ? [
        {
          href: "/area-leader/dashboard",
          icon: LayoutDashboard,
          label: "Area Leader dashboard",
          permission: null,
        },
      ]
    : [];
  const facilitatorLinks = isFacilitator(session?.user?.role)
    ? [
        {
          href: "/facilitator/dashboard",
          icon: LayoutDashboard,
          label: "Facilitator dashboard",
          permission: null,
        },
      ]
    : [];
  const contextualMoreLinks = [...areaLeaderLinks, ...facilitatorLinks, ...moreLinks].filter(
    (link, index, links) => links.findIndex((candidate) => candidate.href === link.href) === index,
  );
  const primaryLinks = isSystemAdmin(session?.user?.role)
    ? [
        { href: "/admin/dashboard", icon: LayoutDashboard, label: "Admin", permission: null },
        {
          href: "/admin/customers",
          icon: Users,
          label: "Admin customers",
          permission: "Aqua.Admin.Customers.View",
        },
        ...mainLinks,
      ]
    : mainLinks;

  const hasPermission = (permission: string | null) => {
    if (!permission) return true;
    return session?.user?.permissions?.includes(permission) ?? false;
  };

  const canCreateCustomer = session?.user?.permissions?.includes("Aqua.Members.Create") ?? false;

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  if (pathname.startsWith("/admin")) {
    return (
      <header className="sticky top-0 z-40 w-full border-b border-border bg-card/95 backdrop-blur">
        <div className="flex h-16 items-center justify-between px-4 sm:px-6 lg:px-8">
          <Link className="flex items-center gap-3 text-foreground transition hover:opacity-80" href="/admin/dashboard">
            <Image
              alt=""
              aria-hidden="true"
              className="size-9 rounded-lg object-cover"
              height={36}
              src="/aqua-lifestyle-logo.jpg"
              width={36}
            />
            <div>
              <p className="font-bold leading-tight">Aqua Lifestyle</p>
              <p className="text-xs text-muted-foreground">Administration</p>
            </div>
          </Link>
          <UserMenu />
        </div>
      </header>
    );
  }

  return (
    <header
      className={cn(
        "sticky top-0 z-40 w-full",
        isLanding
          ? "border-b border-white/10 bg-aqua-navy/95 text-white backdrop-blur-xl"
          : "glass",
      )}
    >
      {isLanding ? (
        <a
          className="sr-only z-50 rounded-aqua-control bg-aqua-surface px-4 py-3 font-semibold text-aqua-ink focus:not-sr-only focus:absolute focus:left-4 focus:top-3"
          href="#landing-title"
        >
          Skip to main content
        </a>
      ) : null}
      <div
        className={cn(
          "flex h-16 items-center justify-between",
          isLanding
            ? landingContainerClassName
            : "mx-auto w-full max-w-7xl px-4 sm:px-6 lg:px-8",
        )}
      >
        <div className="flex items-center gap-8">
          <Link
            className={cn(
              "flex items-center gap-3 transition hover:opacity-80",
              isLanding ? "text-white" : "text-foreground",
            )}
            href="/"
          >
            <Image
              alt=""
              aria-hidden="true"
              className="size-9 rounded-lg object-cover"
              height={36}
              priority={isLanding}
              src="/aqua-lifestyle-logo.jpg"
              width={36}
            />
            <span className="hidden text-lg font-bold tracking-tight sm:inline">
              Aqua Lifestyle Club
            </span>
          </Link>

          <nav
            aria-label="Primary navigation"
            className={cn(
              "items-center gap-1",
              isAuthenticated ? "hidden xl:flex" : "hidden lg:flex",
            )}
          >
            {isAuthenticated
              ? primaryLinks.filter((link) => hasPermission(link.permission)).map((link) => (
                  <Link
                    key={link.href}
                    className={cn(
                      "relative flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition",
                      isActive(link.href)
                        ? "text-accent"
                        : isLanding
                          ? "text-white/70 hover:bg-white/10 hover:text-white"
                          : "text-muted-foreground hover:bg-muted hover:text-foreground",
                    )}
                    href={link.href}
                  >
                    <link.icon className="size-4" />
                    {link.label}
                    {isActive(link.href) ? (
                      <span className="absolute bottom-0 left-1/2 h-0.5 w-6 -translate-x-1/2 rounded-full bg-accent" />
                    ) : null}
                  </Link>
                ))
              : publicLinks.map((link) => (
                  <Link
                    className={cn(
                      "rounded-lg px-3 py-2 text-sm font-semibold transition",
                      isLanding
                        ? "text-white/70 hover:bg-white/10 hover:text-white"
                        : "text-muted-foreground hover:bg-muted hover:text-foreground",
                    )}
                    href={link.href}
                    key={link.href}
                  >
                    {link.label}
                  </Link>
                ))}

            {isAuthenticated ? (
              <div className="relative">
                <button
                  aria-expanded={isMoreOpen}
                  className={cn(
                    "flex items-center gap-1 rounded-lg px-3 py-2 text-sm font-semibold transition",
                    isLanding
                      ? "text-white/70 hover:bg-white/10 hover:text-white"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground",
                    isMoreOpen &&
                      (isLanding ? "bg-white/10 text-white" : "bg-muted text-foreground"),
                  )}
                  onClick={() => setIsMoreOpen((current) => !current)}
                  type="button"
                >
                  More
                  <ChevronDown
                    className={cn(
                      "size-4 transition-transform",
                      isMoreOpen && "rotate-180",
                    )}
                  />
                </button>

                {isMoreOpen ? (
                  <div className="absolute left-0 top-full z-50 mt-2 w-44 rounded-xl border border-border bg-card p-1 shadow-lg animate-fade-in">
                    {contextualMoreLinks
                      .filter((link) => hasPermission(link.permission))
                      .map((link) => (
                        <Link
                          key={link.href}
                          className={cn(
                            "flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition",
                            isActive(link.href)
                              ? "bg-accent/10 text-accent"
                              : "text-foreground hover:bg-muted",
                          )}
                          href={link.href}
                          onClick={() => setIsMoreOpen(false)}
                        >
                          <link.icon className="size-4" />
                          {link.label}
                        </Link>
                      ))}
                  </div>
                ) : null}
              </div>
            ) : null}
          </nav>
        </div>

        <div className="flex items-center gap-3">
          {canCreateCustomer ? (
            <Link
              className={cn(
                "hidden items-center gap-1.5 rounded-aqua-control px-3 py-2 text-sm font-semibold text-white shadow-sm transition sm:inline-flex",
                isLanding
                  ? "bg-aqua-violet hover:bg-aqua-violet-dark"
                  : "bg-accent hover:bg-accent-dark",
              )}
              href="/customers/register"
            >
              <Plus className="size-4" />
              Add customer
            </Link>
          ) : null}

          {isAuthenticated ? (
            <div className="hidden lg:block">
              <TenantSwitcher />
            </div>
          ) : null}

          <UserMenu inverted={isLanding} />

          <button
            aria-controls="mobile-navigation"
            aria-expanded={isMobileOpen}
            aria-label={isMobileOpen ? "Close navigation" : "Open navigation"}
            className={cn(
              "inline-flex rounded-aqua-control p-2 transition",
              isAuthenticated ? "xl:hidden" : "lg:hidden",
              isLanding ? "text-white hover:bg-white/10" : "text-foreground hover:bg-muted",
            )}
            onClick={() => setIsMobileOpen((current) => !current)}
            type="button"
          >
            {isMobileOpen ? <X className="size-6" /> : <Menu className="size-6" />}
          </button>
        </div>
      </div>

      {isMobileOpen ? (
        <div
          id="mobile-navigation"
          className={cn(
            "max-h-[calc(100dvh-4rem)] overflow-y-auto border-t px-4 py-4 animate-fade-in",
            isAuthenticated ? "xl:hidden" : "lg:hidden",
            isLanding ? "border-white/10 bg-aqua-navy" : "border-border bg-card",
          )}
        >
          <nav aria-label="Mobile navigation" className="flex flex-col gap-1">
            {isAuthenticated
              ? [...primaryLinks, ...contextualMoreLinks]
                  .filter((link) => hasPermission(link.permission))
                  .map((link) => (
                    <Link
                      key={link.href}
                      className={cn(
                        "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-semibold transition",
                        isActive(link.href)
                          ? "bg-accent/10 text-accent"
                          : isLanding
                            ? "text-white/80 hover:bg-white/10 hover:text-white"
                            : "text-foreground hover:bg-muted",
                      )}
                      href={link.href}
                      onClick={() => setIsMobileOpen(false)}
                    >
                      <link.icon className="size-5" />
                      {link.label}
                    </Link>
                  ))
              : publicLinks.map((link) => (
                  <Link
                    className={cn(
                      "rounded-lg px-3 py-2.5 text-sm font-semibold transition",
                      isLanding
                        ? "text-white/80 hover:bg-white/10 hover:text-white"
                        : "text-foreground hover:bg-muted",
                    )}
                    href={link.href}
                    key={link.href}
                    onClick={() => setIsMobileOpen(false)}
                  >
                    {link.label}
                  </Link>
                ))}
          </nav>
          {isAuthenticated ? (
            <div
              className={cn(
                "mt-4 flex items-center justify-between border-t pt-4",
                isLanding ? "border-white/10" : "border-border",
              )}
            >
              <TenantSwitcher />
              {canCreateCustomer ? (
                <Link
                  className="inline-flex items-center gap-1.5 rounded-lg bg-accent px-3 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark"
                  href="/customers/register"
                >
                  <Plus className="size-4" />
                  Add customer
                </Link>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}
    </header>
  );
};
