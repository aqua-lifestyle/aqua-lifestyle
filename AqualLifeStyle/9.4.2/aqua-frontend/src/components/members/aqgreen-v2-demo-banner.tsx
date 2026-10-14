"use client";

import { useEffect, useState } from "react";

import {
  isAQGreenV2DemoCheckout,
  type SystemHealth,
} from "@/src/providers/SystemHealth/contract";
import { apiEndpoints, httpClient } from "@/src/shared/api";

export const AQGreenV2DemoBanner = () => {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    void httpClient.get<SystemHealth>(apiEndpoints.health.get)
      .then((health) => setVisible(Boolean(isAQGreenV2DemoCheckout(health))))
      .catch(() => setVisible(false));
  }, []);

  if (!visible) return null;
  return (
    <aside className="border-y-2 border-amber-700 bg-amber-100 px-4 py-3 text-center text-sm font-bold text-amber-950" role="note">
      TEST ENVIRONMENT — SIMULATED MONEY — NO REAL PAYMENT OR EARNINGS
      <span className="block text-xs font-medium">
        All checkout, sales and commission amounts here are test records. No payout can be released or marked paid.
      </span>
    </aside>
  );
};
