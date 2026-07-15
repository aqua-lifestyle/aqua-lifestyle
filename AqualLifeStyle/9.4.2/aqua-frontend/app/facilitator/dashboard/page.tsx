import { FacilitatorDashboard } from "@/src/components/facilitators/facilitator-dashboard";
import { FacilitatorGuard } from "@/src/features/facilitator/ui/facilitator-guard";

export default function FacilitatorDashboardPage() {
  return (
    <FacilitatorGuard>
      <FacilitatorDashboard />
    </FacilitatorGuard>
  );
}
