import { apiClient } from "./axios-instance";

export const httpClient = {
  get: async <TResponse>(url: string) => {
    const response = await apiClient.get<TResponse>(url);
    return response.data;
  },

  post: async <TResponse, TBody>(url: string, body: TBody) => {
    const response = await apiClient.post<TResponse>(url, body);
    return response.data;
  },

  put: async <TResponse, TBody>(url: string, body: TBody) => {
    const response = await apiClient.put<TResponse>(url, body);
    return response.data;
  },

  delete: async <TResponse>(url: string) => {
    const response = await apiClient.delete<TResponse>(url);
    return response.data;
  },
};
