"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { MyProgrammeParticipations } from "@/src/shared/domain/programme-participations";

export const useMyProgrammeParticipations = (enabled: boolean) => {
  const [data, setData] = useState<MyProgrammeParticipations>();
  const [errorMessage, setErrorMessage] = useState<string>();
  const [isLoading, setIsLoading] = useState(enabled);
  const enabledRef = useRef(enabled);
  const requestIdentifierRef = useRef(0);

  const reload = useCallback(async () => {
    const requestIdentifier = ++requestIdentifierRef.current;
    if (!enabled) {
      setData(undefined);
      setErrorMessage(undefined);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setErrorMessage(undefined);
    try {
      const result = await httpClient.get<MyProgrammeParticipations>(
        apiEndpoints.programmeParticipations.getMyParticipations,
      );
      if (
        requestIdentifier === requestIdentifierRef.current &&
        enabledRef.current
      ) {
        setData(result);
      }
    } catch (error) {
      if (
        requestIdentifier === requestIdentifierRef.current &&
        enabledRef.current
      ) {
        setErrorMessage(
          getRequestErrorMessage(
            error,
            "Your programme participation could not be loaded.",
          ),
        );
      }
    } finally {
      if (
        requestIdentifier === requestIdentifierRef.current &&
        enabledRef.current
      ) {
        setIsLoading(false);
      }
    }
  }, [enabled]);

  useEffect(() => {
    enabledRef.current = enabled;
    if (!enabled) {
      requestIdentifierRef.current += 1;
    }

    const task = window.setTimeout(() => {
      if (enabled) {
        void reload();
        return;
      }
      setData(undefined);
      setErrorMessage(undefined);
      setIsLoading(false);
    }, 0);
    return () => window.clearTimeout(task);
  }, [enabled, reload]);

  return { data, errorMessage, isLoading, reload };
};
