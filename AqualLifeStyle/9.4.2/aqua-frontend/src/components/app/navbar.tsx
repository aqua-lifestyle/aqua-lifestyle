"use client";

import {
  Building2,
  ChevronDown,
  Droplets,
  Home,
  LayoutDashboard,
  Menu,
  MessageSquare,
  Package,
  Plus,
  Users,
  UserPlus,
  DollarSign,
  X,
  User,
  Mail,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { cn } from "@/src/shared/lib/utils";

import { useAuthState } from "@/src/providers";
import { TenantSwitcher } from "./tenant-switcher";
import { UserMenu } from "./user-menu";

const mainLinks = [
  { href: "/", icon: LayoutDashboard, label: "Dashboard", permission: null },
  { href: "/customers", icon: Users, label: "Customers", permission: "Pages.Customers" },
  { href: "/products", icon: Package, label: "Products", permission: "Pages.Products" },
  { href: "/enquiries", icon: MessageSquare, label: "Enquiries", permission: "Pages.Enquiries" },
];

const moreLinks = [
  { href: "/memberships", icon: Building2, label: "Memberships", permission: "Pages.Memberships" },
  { href: "/order-intents", icon: Home, label: "Order intents", permission: "Pages.Orders" },
  { href: "/area-leader", icon: Users, label: "Area Leaders", permission: "Pages.AreaLeaders" },
  { href: "/facilitator", icon: UserPlus, label: "Facilitators", permission: "Pages.Facilitators" },
  { href: "/facilitator/referrals", icon: DollarSign, label: "Referrals", permission: "Pages.Referrals" },
  { href: "/member", icon: User, label: "Member", permission: "Pages.Orders" },
  { href: "/catalog", icon: Package, label: "Catalog", permission: null },
  { href: "/contact", icon: Mail, label: "Contact", permission: null },
];

export const Navbar = () => {
  const pathname = usePathname();
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const [isMoreOpen, setIsMoreOpen] = useState(false);
  const { session } = useAuthState();

  const hasPermission = (permission: string | null) => {
    if (!permission) return true;
    return session?.user?.permissions?.includes(permission) ?? false;
  };

  const canCreateCustomer = session?.user?.permissions?.includes("Aqua.Members.Create") ?? false;

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <header className="sticky top-0 z-40 w-full glass">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        <div className="flex items-center gap-8">
          <Link
            className="flex items-center gap-2 text-foreground transition hover:opacity-80"
            href="/"
          >
            <div className="flex size-9 items-center justify-center rounded-xl bg-gradient-to-br from-accent to-accent-dark text-white shadow-md">
              <Droplets className="size-5" />
            </div>
            <span className="hidden text-lg font-bold tracking-tight sm:inline">
              Aqua Lifestyle
            </span>
          </Link>

          <nav className="hidden items-center gap-1 lg:flex">
            {mainLinks.filter((link) => hasPermission(link.permission)).map((link) => (
              <Link
                key={link.href}
                className={cn(
                  "relative flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition",
                  isActive(link.href)
                    ? "text-accent"
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
            ))}

            <div className="relative">
              <button
                aria-expanded={isMoreOpen}
                className={cn(
                  "flex items-center gap-1 rounded-lg px-3 py-2 text-sm font-semibold text-muted-foreground transition hover:bg-muted hover:text-foreground",
                  isMoreOpen && "bg-muted text-foreground",
                )}
                onClick={() => setIsMoreOpen((current) => !current)}
                type="button"
              >
                More
                <ChevronDown
                  className={cn("size-4 transition-transform", isMoreOpen && "rotate-180")}
                />
              </button>

              {isMoreOpen ? (
                <div className="absolute left-0 top-full z-50 mt-2 w-44 rounded-xl border border-border bg-card p-1 shadow-lg animate-fade-in">
                  {moreLinks.map((link) => (
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
          </nav>
        </div>

        <div className="flex items-center gap-3">
          {canCreateCustomer ? (
            <Link
              className="hidden items-center gap-1.5 rounded-lg bg-accent px-3 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-accent-dark sm:inline-flex"
              href="/customers/register"
            >
              <Plus className="size-4" />
              Add customer
            </Link>
          ) : null}

          <div className="hidden lg:block">
            <TenantSwitcher />
          </div>

          <UserMenu />

          <button
            aria-label="Toggle menu"
            className="inline-flex rounded-lg p-2 text-foreground transition hover:bg-muted lg:hidden"
            onClick={() => setIsMobileOpen((current) => !current)}
            type="button"
          >
            {isMobileOpen ? <X className="size-6" /> : <Menu className="size-6" />}
          </button>
        </div>
      </div>

      {isMobileOpen ? (
        <div className="border-t border-border bg-card px-4 py-4 lg:hidden animate-fade-in">
          <nav className="flex flex-col gap-1">
            {[...mainLinks, ...moreLinks].filter((link) => hasPermission(link.permission)).map((link) => (
              <Link
                key={link.href}
                className={cn(
                  "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-semibold transition",
                  isActive(link.href)
                    ? "bg-accent/10 text-accent"
                    : "text-foreground hover:bg-muted",
                )}
                href={link.href}
                onClick={() => setIsMobileOpen(false)}
              >
                <link.icon className="size-5" />
                {link.label}
              </Link>
            ))}
          </nav>
          <div className="mt-4 flex items-center justify-between border-t border-border pt-4">
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
        </div>
      ) : null}
    </header>
  );
};
