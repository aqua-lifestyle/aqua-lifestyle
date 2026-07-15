import type { Metadata } from "next";
import { AdminTenants } from "@/src/components/admin/AdminTenants";
export const metadata: Metadata = { title: "Tenant Management | Aqua Lifestyle Club" };
export default function AdminTenantsPage() { return <AdminTenants />; }
