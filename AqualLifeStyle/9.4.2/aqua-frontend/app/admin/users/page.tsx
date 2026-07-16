import type { Metadata } from "next";
import { AdminUsers } from "@/src/components/admin/AdminUsers";

export const metadata: Metadata = { title: "User Management | Aqua Lifestyle Club" };
export default function AdminUsersPage() { return <AdminUsers />; }
