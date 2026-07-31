import type { Metadata } from "next";

import { LandingPage } from "@/src/components/app/landing-page";

export const metadata: Metadata = {
  description:
    "Explore Aqua Lifestyle Club membership, aQuathz products, community programmes and connected member pathways.",
  title: "Aqua Lifestyle Club | Live in health. Inspire to wealth.",
};

export default function Home() {
  return <LandingPage />;
}
