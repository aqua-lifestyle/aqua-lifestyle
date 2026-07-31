import type { Metadata } from "next";

import { LandingPage } from "@/src/components/app/landing-page";

export const metadata: Metadata = {
  alternates: {
    canonical: "/",
  },
  description:
    "Explore Aqua Lifestyle Club membership, aQuathz products, community programmes and connected member pathways.",
  openGraph: {
    description:
      "Explore Aqua Lifestyle Club membership, aQuathz products, community programmes and connected member pathways.",
    title: "Live in health. Inspire to wealth.",
    url: "/",
  },
  title: "Live in health. Inspire to wealth. | Aqua Lifestyle Club",
  twitter: {
    card: "summary_large_image",
    description:
      "Explore Aqua Lifestyle Club membership, aQuathz products, community programmes and connected member pathways.",
    title: "Live in health. Inspire to wealth.",
  },
};

export default function Home() {
  return <LandingPage />;
}
