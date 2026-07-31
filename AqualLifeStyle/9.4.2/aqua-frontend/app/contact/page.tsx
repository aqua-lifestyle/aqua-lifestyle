import type { Metadata } from "next";

import { ContactPage } from "@/src/components/guest/contact-page";

export const metadata: Metadata = {
  description:
    "Find product, registration and member support paths for Aqua Lifestyle Club.",
  title: "Help and Support | Aqua Lifestyle Club",
};

export default function ContactUsPage() {
  return <ContactPage />;
}
