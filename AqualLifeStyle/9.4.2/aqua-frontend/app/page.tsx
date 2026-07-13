"use client";

import { CustomerDashboard } from "@/src/components/demo/customer-dashboard";
import { DemoDashboard } from "@/src/components/demo/demo-dashboard";
import { useAuthState } from "@/src/providers";

export default function Home() {
  const { session } = useAuthState();
  const role = session?.user?.role?.toLowerCase();

  if (role === "admin" || role === "systemadmin") {
    return <DemoDashboard />;
  }

  return <CustomerDashboard />;
}
