"use client";

import { useEffect, useMemo, useState } from "react";

import { useCustomersActions, useCustomersState, useFacilitatorsActions, useFacilitatorsState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { Breadcrumb, StatusMessage } from "@/src/shared/ui";
import { FacilitatorApproval } from "./facilitator-approval";

export const FacilitatorManagement = () => {
  const customers = useCustomersState(); const customerActions = useCustomersActions();
  const facilitators = useFacilitatorsState(); const facilitatorActions = useFacilitatorsActions();
  const [actionError, setActionError] = useState<string | null>(null);
  const failed = customers.isLoadError || facilitators.isLoadError;

  useEffect(() => {
    void Promise.all([customerActions.getCustomers(), facilitatorActions.getFacilitators()]);
  }, [customerActions, facilitatorActions]);

  const pending = useMemo(() => {
    const names = new Map(customers.customers.map((customer) => [customer.id, customer.name]));
    return facilitators.facilitators.filter((item) => item.isApproved === false).map((item) => ({
      customerName: names.get(item.customerId) ?? `Applicant #${item.customerId}`,
      directReferrals: item.directReferrals,
      id: item.id,
    }));
  }, [customers.customers, facilitators.facilitators]);

  const approve = async (id: number) => {
    setActionError(null);
    if (failed) {
      setActionError("Live Area data must load successfully before approving facilitators.");
      return;
    }
    try {
      await httpClient.post(`/api/services/app/Facilitator/Approve?id=${id}`, {});
      await facilitatorActions.getFacilitators();
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "Unable to approve the facilitator.");
    }
  };

  return (
    <main className="min-h-[calc(100dvh-7.5rem)] bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-5xl flex-col gap-6">
        <header><Breadcrumb items={[{ href: "/area-leader/dashboard", label: "Area Leader" }, { label: "Facilitators" }]} /><h1 className="mt-2 text-3xl font-bold tracking-tight">Facilitator applications</h1><p className="mt-2 text-muted-foreground">Review applicants assigned to your Area Space.</p></header>
        {failed ? <StatusMessage tone="error">Live facilitator applications could not be loaded. Refresh before approving an applicant.</StatusMessage> : null}
        {actionError ? <StatusMessage tone="error">{actionError}</StatusMessage> : null}
        <FacilitatorApproval facilitators={pending} onApprove={approve} />
      </div>
    </main>
  );
};
