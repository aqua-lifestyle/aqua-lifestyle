"use client";

import { useEffect, useState } from "react";
import { apiClient } from "@/src/shared/api";
import { unwrapAbpResponse } from "@/src/shared/api/abp-error";

export function usePilotStatus() {
  const [status, setStatus] = useState<{ isPilot: boolean; testMoneyOnly: boolean }>();
  const [isRecovery, setRecovery] = useState(false);
  useEffect(() => {
    let active = true;
    void apiClient.get("/api/services/app/AQGreenPilot/GetStatus", {
      // Inspect only this expected suspension response without treating it as success.
      validateStatus: (code) => code === 200 || code === 503,
    }).then((response) => {
      if (!active) return;
      if (response.status === 503 && response.headers["x-aqua-recovery-suspended"] === "true") {
        setRecovery(true);
        return;
      }
      if (response.status !== 200) return;
      const value = unwrapAbpResponse<{ isPilot: boolean; testMoneyOnly: boolean }>(response.data);
      if (typeof value?.isPilot === "boolean" &&
          typeof value.testMoneyOnly === "boolean" && value.isPilot === value.testMoneyOnly)
        setStatus(value);
    }).catch(() => {
      // Unknown deployment mode must not enable money or uncommissioned actions.
    });
    return () => { active = false; };
  }, []);
  return { isPilot: status?.isPilot === true, modeKnown: status !== undefined, isRecovery };
}
