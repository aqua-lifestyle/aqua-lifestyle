"use client";

import { useEffect, useState } from "react";

import { getTenantSelfRegistrationAvailability } from "@/src/shared/api/auth-service";

export type TenantSelfRegistrationAvailability =
  | "disabled"
  | "enabled"
  | "loading"
  | "unavailable";

export const useTenantSelfRegistrationAvailability = (tenancyName: string) => {
  const normalizedTenancyName = tenancyName.trim();
  const [loadedAvailability, setLoadedAvailability] = useState<{
    availability: TenantSelfRegistrationAvailability;
    tenancyName: string;
  }>({ availability: "loading", tenancyName: "" });

  useEffect(() => {
    if (!normalizedTenancyName) {
      return;
    }

    let isCurrentRequest = true;

    void getTenantSelfRegistrationAvailability(normalizedTenancyName).then((result) => {
      if (!isCurrentRequest) return;

      setLoadedAvailability({
        availability: result.ok
          ? result.isSelfRegistrationEnabled
            ? "enabled"
            : "disabled"
          : "unavailable",
        tenancyName: normalizedTenancyName,
      });
    });

    return () => {
      isCurrentRequest = false;
    };
  }, [normalizedTenancyName]);

  if (!normalizedTenancyName) {
    return "disabled";
  }

  return loadedAvailability.tenancyName === normalizedTenancyName
    ? loadedAvailability.availability
    : "loading";
};
