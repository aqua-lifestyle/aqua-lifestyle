import type { Metadata } from "next";
import { AdminTenants } from "@/src/components/admin/AdminTenants";
export const metadata: Metadata = { title: "Area Management | Aqua Lifestyle Club" };
export default function AdminTenantsPage() { return <AdminTenants />; }
