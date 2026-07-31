import type { Metadata } from "next";

import { LandingPage } from "@/src/components/app/landing-page";

export const metadata: Metadata = {
  description:
    "Explore Aqua Lifestyle Club membership, aQuathz products, community programmes and member opportunities.",
  title: "Aqua Lifestyle Club | Live well. Grow together.",
};

export default function Home() {
  return <LandingPage />;
}
