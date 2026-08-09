"use client";

import { useCallback, useEffect, useState } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";

export const PROGRAMME_APPROVAL_QUEUE_CHANGED =
  "programme-approval-queue-changed";

export type PendingProgrammeApprovalSummary = {
  aqGreenCount: number;
  onyxCount: number;
  totalCount: number;
};

export const usePendingProgrammeApprovals = (enabled: boolean) => {
  const [summary, setSummary] =
    useState<PendingProgrammeApprovalSummary>();

  const reload = useCallback(async () => {
    if (!enabled) {
      setSummary(undefined);
      return;
    }

    try {
      setSummary(
        await httpClient.get<PendingProgrammeApprovalSummary>(
          apiEndpoints.programmeParticipations.getPendingApprovalSummary,
        ),
      );
    } catch {
      // The durable queue page remains authoritative. A badge failure must not
      // hide the navigation item or block the rest of the administrator UI.
      setSummary(undefined);
    }
  }, [enabled]);

  useEffect(() => {
    const task = window.setTimeout(() => void reload(), 0);
    const refresh = () => void reload();
    window.addEventListener(PROGRAMME_APPROVAL_QUEUE_CHANGED, refresh);
    window.addEventListener("focus", refresh);
    return () => {
      window.clearTimeout(task);
      window.removeEventListener(PROGRAMME_APPROVAL_QUEUE_CHANGED, refresh);
      window.removeEventListener("focus", refresh);
    };
  }, [reload]);

  return { reload, summary };
};
