"use client";

import { useEffect, useState } from "react";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { SelectField, StatusMessage, TextField } from "@/src/shared/ui";

type AreaOption = { id: number; name: string; tenancyName: string };
type PagedResult<T> = { items: T[]; totalCount: number };

type AdminAreaSelectionFieldProps = {
  className?: string;
  errorMessage?: string;
  fixedAreaId?: number;
  onChange?: (areaId: string) => void;
  value?: string;
};

export const AdminAreaSelectionField = ({
  className,
  errorMessage,
  fixedAreaId,
  onChange,
  value,
}: AdminAreaSelectionFieldProps) => {
  const [areas, setAreas] = useState<AreaOption[]>([]);
  const [loadError, setLoadError] = useState<string>();

  useEffect(() => {
    if (fixedAreaId) return;

    void httpClient
      .get<PagedResult<AreaOption>>("/api/services/app/AdminTenant/GetAll?IsActive=true&MaxResultCount=100")
      .then((result) => {
        setAreas(result.items);
        setLoadError(undefined);
      })
      .catch((requestError) => {
        setLoadError(getRequestErrorMessage(requestError, "Available areas could not be loaded."));
      });
  }, [fixedAreaId]);

  if (fixedAreaId) {
    return (
      <>
        <TextField className={className} disabled label="Area" name="selectedArea" value={`Your area (${fixedAreaId})`} />
        <input name="tenantId" type="hidden" value={fixedAreaId} />
      </>
    );
  }

  return (
    <div className={className}>
      <SelectField
        errorMessage={errorMessage}
        label="Area"
        name="tenantId"
        onChange={(event) => onChange?.(event.target.value)}
        required
        value={value}
      >
        <option value="">Select an area</option>
        {areas.map((area) => (
          <option key={area.id} value={area.id}>{area.name} ({area.tenancyName})</option>
        ))}
      </SelectField>
      {loadError ? <StatusMessage className="mt-2" tone="error">{loadError}</StatusMessage> : null}
    </div>
  );
};
