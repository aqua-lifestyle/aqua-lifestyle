"use client";

import { useEffect, useState } from "react";
import { httpClient } from "@/src/shared/api";

export function usePilotStatus() {
  const [status, setStatus] = useState<{ isPilot: boolean; testMoneyOnly: boolean }>();
  useEffect(() => {
    let active = true;
    void httpClient.get<{ isPilot: boolean; testMoneyOnly: boolean }>(
      "/api/services/app/AQGreenPilot/GetStatus",
    ).then((value) => {
      if (active && typeof value?.isPilot === "boolean" &&
          typeof value.testMoneyOnly === "boolean" && value.isPilot === value.testMoneyOnly)
        setStatus(value);
    }).catch(() => {
      // Unknown deployment mode must not enable money or uncommissioned actions.
    });
    return () => { active = false; };
  }, []);
  return { isPilot: status?.isPilot === true, modeKnown: status !== undefined };
}
