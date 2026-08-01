"use client";

import { ArrowRight } from "lucide-react";

import { useAuthState } from "@/src/providers";
import { getRoleHome } from "@/src/shared/auth/roles";
import { LandingLinkButton } from "./landing-primitives";

export const LandingAccountActions = () => {
  const { session } = useAuthState();
  const home = getRoleHome(session?.user?.role);

  return (
    <div className="mt-9 flex flex-col justify-center gap-3 sm:flex-row sm:flex-wrap">
      {session?.user ? (
        <>
          <LandingLinkButton href={home.href}>
            {home.label}
            <ArrowRight aria-hidden="true" className="size-4" />
          </LandingLinkButton>
          <LandingLinkButton href="/catalog" tone="secondary-dark">
            Browse products
          </LandingLinkButton>
        </>
      ) : (
        <>
          <LandingLinkButton href="/catalog">
            Browse products
            <ArrowRight aria-hidden="true" className="size-4" />
          </LandingLinkButton>
          <LandingLinkButton href="/signup" tone="secondary-dark">
            Create an account
          </LandingLinkButton>
          <LandingLinkButton className="border-transparent" href="/login" tone="secondary-dark">
            Sign in
          </LandingLinkButton>
        </>
      )}
    </div>
  );
};
