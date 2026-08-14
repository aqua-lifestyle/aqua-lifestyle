import type { ReactNode } from "react";

import { AuthenticatedPage } from "@/src/components/auth/authenticated-page";

export default function MemberLayout({ children }: { children: ReactNode }) {
  return <AuthenticatedPage>{children}</AuthenticatedPage>;
}
