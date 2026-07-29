"use client";

import { useCallback, useEffect, useState } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { MyProgrammeParticipations } from "@/src/shared/domain/programme-participations";

export const useMyProgrammeParticipations = (enabled: boolean) => {
  const [data, setData] = useState<MyProgrammeParticipations>();
  const [errorMessage, setErrorMessage] = useState<string>();
  const [isLoading, setIsLoading] = useState(enabled);

  const reload = useCallback(async () => {
    if (!enabled) {
      setData(undefined);
      setErrorMessage(undefined);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setErrorMessage(undefined);
    try {
      setData(
        await httpClient.get<MyProgrammeParticipations>(
          apiEndpoints.programmeParticipations.getMyParticipations,
        ),
      );
    } catch (error) {
      setErrorMessage(
        getRequestErrorMessage(
          error,
          "Your programme participation could not be loaded.",
        ),
      );
    } finally {
      setIsLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    const task = window.setTimeout(() => void reload(), 0);
    return () => window.clearTimeout(task);
  }, [reload]);

  return { data, errorMessage, isLoading, reload };
};
