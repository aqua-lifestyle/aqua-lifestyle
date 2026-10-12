import { AuthenticatedPage } from "@/src/components/auth/authenticated-page";
import { AQGreenV2TestCheckout } from "@/src/components/members/aqgreen-v2-test-checkout";

export default function AQGreenV2TestCheckoutPage() {
  return (
    <AuthenticatedPage>
      <AQGreenV2TestCheckout />
    </AuthenticatedPage>
  );
}
