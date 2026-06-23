import type { ReactNode } from "react";

import { AdminSidebar } from "@/src/components/admin/AdminSidebar";
import { AdminGuard } from "@/src/features/admin/ui/admin-guard";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return (
    <AdminGuard>
      <div className="min-h-[calc(100dvh-4rem)] bg-muted/30 lg:flex">
        <AdminSidebar />
        <div className="min-w-0 flex-1">{children}</div>
      </div>
    </AdminGuard>
  );
}
