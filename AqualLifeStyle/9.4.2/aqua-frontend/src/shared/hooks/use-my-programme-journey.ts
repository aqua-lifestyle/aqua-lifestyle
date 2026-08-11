"use client";

import { useEffect, useRef, useState } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { MyProgrammeJourney } from "@/src/shared/domain/programme-journey";

export const useMyProgrammeJourney = (enabled: boolean) => {
  const requestId = useRef(0);
  const [data, setData] = useState<MyProgrammeJourney>();
  const [errorMessage, setErrorMessage] = useState<string>();
  const [isLoading, setIsLoading] = useState(enabled);

  useEffect(() => {
    const task = window.setTimeout(() => {
      if (!enabled) {
        requestId.current += 1;
        setData(undefined);
        setErrorMessage(undefined);
        setIsLoading(false);
        return;
      }

      const currentRequest = ++requestId.current;
      setIsLoading(true);
      setErrorMessage(undefined);
      void httpClient
        .get<MyProgrammeJourney>(
          apiEndpoints.programmeParticipations.getMyJourney,
        )
        .then((result) => {
          if (requestId.current === currentRequest) setData(result);
        })
        .catch((error) => {
          if (requestId.current === currentRequest) {
            setErrorMessage(
              getRequestErrorMessage(
                error,
                "Your programme journey could not be loaded.",
              ),
            );
          }
        })
        .finally(() => {
          if (requestId.current === currentRequest) setIsLoading(false);
        });
    }, 0);
    return () => window.clearTimeout(task);
  }, [enabled]);

  return { data, errorMessage, isLoading };
};
