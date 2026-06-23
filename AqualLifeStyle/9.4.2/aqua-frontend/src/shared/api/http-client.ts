import { apiClient } from "./axios-instance";
import { unwrapAbpResponse, type AbpResponseEnvelope } from "./abp-error";

export const httpClient = {
  get: async <TResponse>(url: string) => {
    const response = await apiClient.get<TResponse | AbpResponseEnvelope<TResponse>>(url);
    return unwrapAbpResponse(response.data);
  },

  post: async <TResponse, TBody>(url: string, body: TBody) => {
    const response = await apiClient.post<TResponse | AbpResponseEnvelope<TResponse>>(url, body);
    return unwrapAbpResponse(response.data);
  },

  put: async <TResponse, TBody>(url: string, body: TBody) => {
    const response = await apiClient.put<TResponse | AbpResponseEnvelope<TResponse>>(url, body);
    return unwrapAbpResponse(response.data);
  },

  delete: async <TResponse, TBody = undefined>(url: string, body?: TBody) => {
    const response = await apiClient.delete<TResponse | AbpResponseEnvelope<TResponse>>(url, {
      // ABP's conventional DELETE actions bind complex input DTOs from the
      // query string. A JSON request body reaches the route but is ignored.
      params: body,
    });
    return unwrapAbpResponse(response.data);
  },
};
