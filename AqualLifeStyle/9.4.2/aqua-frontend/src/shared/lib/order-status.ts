export type OrderStatusTone = "neutral" | "info" | "success" | "error";

const ORDER_STATUS_LABELS = ["Draft", "Reserved", "Cancelled", "Completed"];

export const getOrderStatusLabel = (status: number) =>
  ORDER_STATUS_LABELS[status] ?? `Status ${status}`;

export const getOrderStatusTone = (status: number): OrderStatusTone => {
  if (status === 3) return "success";
  if (status === 2) return "error";
  if (status === 1) return "info";
  return "neutral";
};
