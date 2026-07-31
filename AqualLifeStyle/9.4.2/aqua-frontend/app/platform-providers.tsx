"use client";

import type { ReactNode } from "react";

import { AreaLeadersProvider } from "@/src/providers/AreaLeaders";
import { AreaSpacesProvider } from "@/src/providers/AreaSpaces";
import { CustomersProvider } from "@/src/providers/Customers";
import { EnquiriesProvider } from "@/src/providers/Enquiries";
import { FacilitatorsProvider } from "@/src/providers/Facilitators";
import { MembershipsProvider } from "@/src/providers/Memberships";
import { OrderIntentsProvider } from "@/src/providers/OrderIntents";
import { ProductsProvider } from "@/src/providers/Products";
import { ReferralsProvider } from "@/src/providers/Referrals";
import { SystemHealthProvider } from "@/src/providers/SystemHealth";

export const PlatformProviders = ({
  children,
  dataScope,
}: {
  children: ReactNode;
  dataScope: string;
}) => (
  <SystemHealthProvider>
    <AreaLeadersProvider key={`area-leaders-${dataScope}`}>
      <AreaSpacesProvider key={`area-spaces-${dataScope}`}>
        <FacilitatorsProvider key={`facilitators-${dataScope}`}>
          <ReferralsProvider key={`referrals-${dataScope}`}>
            <CustomersProvider key={`customers-${dataScope}`}>
              <EnquiriesProvider key={`enquiries-${dataScope}`}>
                <MembershipsProvider key={`memberships-${dataScope}`}>
                  <ProductsProvider key={`products-${dataScope}`}>
                    <OrderIntentsProvider key={`order-intents-${dataScope}`}>
                      {children}
                    </OrderIntentsProvider>
                  </ProductsProvider>
                </MembershipsProvider>
              </EnquiriesProvider>
            </CustomersProvider>
          </ReferralsProvider>
        </FacilitatorsProvider>
      </AreaSpacesProvider>
    </AreaLeadersProvider>
  </SystemHealthProvider>
);
