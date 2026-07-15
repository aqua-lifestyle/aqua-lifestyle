import type { ReactNode } from "react";

import { AdminGuard } from "@/src/features/admin/ui/admin-guard";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <AdminGuard>{children}</AdminGuard>;
}
