import axios from "axios";

import {
  normalizeAbpError,
  normalizeNetworkError,
  unwrapAbpResponse,
  type AbpErrorEnvelope,
  type AbpResponseEnvelope,
} from "@/src/shared/api/abp-error";
import { apiEndpoints } from "@/src/shared/api/endpoints";
import { publicEnv } from "@/src/shared/config";

export type InternalAccountInvitationPreview = {
  accessLevel: string;
  areaDisplayName: string;
  areaName: string;
  expiresAt: string;
  inviteeName: string;
  status: "Pending";
  username: string;
};

export type InternalAccountInvitationAcceptance = {
  areaName: string;
  wasAlreadyAccepted: boolean;
};

const post = async <TResponse>(path: string, body: unknown): Promise<TResponse> => {
  let data: TResponse | AbpResponseEnvelope<TResponse>;
  try {
    const response = await axios.post<TResponse | AbpResponseEnvelope<TResponse>>(
      `${publicEnv.NEXT_PUBLIC_ABP_API_URL}${path}`,
      body,
    );
    data = response.data;
  } catch (error) {
    if (axios.isAxiosError<AbpErrorEnvelope>(error) && error.response) {
      throw normalizeAbpError(error.response.status, error.response.data);
    }
    throw normalizeNetworkError(error);
  }
  return unwrapAbpResponse(data);
};

export const validateInternalAccountInvitation = (invitationCode: string, setupToken: string) =>
  post<InternalAccountInvitationPreview>(apiEndpoints.internalAccountInvitation.validate, {
    invitationCode,
    setupToken,
  });

export const acceptInternalAccountInvitation = (
  invitationCode: string,
  setupToken: string,
  newPassword: string,
) => post<InternalAccountInvitationAcceptance>(apiEndpoints.internalAccountInvitation.accept, {
  invitationCode,
  newPassword,
  setupToken,
});
