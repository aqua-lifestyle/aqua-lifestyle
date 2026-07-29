import axios from "axios";

import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { apiEndpoints } from "@/src/shared/api/endpoints";
import { publicEnv } from "@/src/shared/config";

const post = async (path: string, body: unknown) => {
  try {
    await axios.post(`${publicEnv.NEXT_PUBLIC_ABP_API_URL}${path}`, body);
    return { ok: true as const };
  } catch (error) {
    return {
      message: getRequestErrorMessage(error, "The request could not be completed. Please try again."),
      ok: false as const,
    };
  }
};

export const confirmEmail = (tenantId: number, userId: number, token: string) =>
  post(apiEndpoints.account.confirmEmail, { tenantId, token, userId });

export const resendEmailVerification = (areaName: string, emailAddress: string, redirectPath?: string) =>
  post(apiEndpoints.account.resendEmailVerification, { areaName, emailAddress, redirectPath });

export const requestPasswordReset = (areaName: string, emailAddress: string) =>
  post(apiEndpoints.account.requestPasswordReset, { areaName, emailAddress });

export const completePasswordReset = (
  tenantId: number,
  userId: number,
  token: string,
  newPassword: string,
) => post(apiEndpoints.account.resetPassword, { newPassword, tenantId, token, userId });
