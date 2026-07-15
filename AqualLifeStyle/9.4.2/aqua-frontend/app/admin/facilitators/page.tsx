import type { Metadata } from "next";
import { AdminFacilitators } from "@/src/components/admin/AdminFacilitators";

export const metadata: Metadata = { title: "Facilitator Management | Aqua Lifestyle Club" };
export default function AdminFacilitatorsPage() { return <AdminFacilitators />; }
