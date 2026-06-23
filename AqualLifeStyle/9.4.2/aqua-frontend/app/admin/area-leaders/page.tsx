import type { Metadata } from "next";
import { AdminAreaLeaders } from "@/src/components/admin/AdminAreaLeaders";

export const metadata: Metadata = { title: "Area Leader Management | Aqua Lifestyle Club" };
export default function AdminAreaLeadersPage() { return <AdminAreaLeaders />; }
