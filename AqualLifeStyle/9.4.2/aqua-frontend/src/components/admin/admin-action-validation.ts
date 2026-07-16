import { z } from "zod";

export const adminAuditJustificationSchema = z.string()
  .trim()
  .min(3, "Explain why this action is required.")
  .max(500);
