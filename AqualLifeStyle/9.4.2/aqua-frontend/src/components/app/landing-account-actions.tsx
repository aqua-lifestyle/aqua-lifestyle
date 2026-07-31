"use client";

import { ArrowRight } from "lucide-react";

import { useAuthState } from "@/src/providers";
import { getRoleHome } from "@/src/shared/auth/roles";
import { LinkButton } from "@/src/shared/ui";

export const LandingAccountActions = () => {
  const { session } = useAuthState();
  const home = getRoleHome(session?.user?.role);

  return (
    <div className="mt-9 flex flex-col justify-center gap-3 sm:flex-row">
      {session?.user ? (
        <LinkButton
          className="rounded-full bg-[#7540e8] px-7 text-white shadow-none hover:bg-[#8655ef]"
          href={home.href}
          size="lg"
        >
          {home.label}
          <ArrowRight aria-hidden="true" className="size-4" />
        </LinkButton>
      ) : (
        <>
          <LinkButton
            className="rounded-full bg-[#7540e8] px-7 text-white shadow-none hover:bg-[#8655ef]"
            href="/signup"
            size="lg"
          >
            Create an account
            <ArrowRight aria-hidden="true" className="size-4" />
          </LinkButton>
          <LinkButton
            className="rounded-full border-white/20 bg-white/5 px-7 text-white hover:bg-white/10"
            href="/login"
            size="lg"
            variant="outline"
          >
            Sign in
          </LinkButton>
        </>
      )}
    </div>
  );
};
