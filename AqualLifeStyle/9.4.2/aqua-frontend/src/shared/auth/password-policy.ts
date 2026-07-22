import { z } from "zod";

export const passwordPolicyDescription =
  "Use at least 8 characters with uppercase, lowercase, number, and one of !@#$%^&*(). Other symbols are not allowed.";

export const securePasswordSchema = z
  .string()
  .min(8, "Use at least 8 characters.")
  .regex(/[A-Z]/, "Add an uppercase letter.")
  .regex(/[a-z]/, "Add a lowercase letter.")
  .regex(/[0-9]/, "Add a number.")
  .regex(/[!@#$%^&*()]/, "Add one of these special characters: !@#$%^&*().")
  .regex(/^[0-9A-Za-z!@#$%^&*()]+$/, "Use only letters, numbers, or !@#$%^&*().");
