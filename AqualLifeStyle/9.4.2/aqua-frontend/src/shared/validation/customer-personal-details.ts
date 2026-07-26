import { z } from "zod";

export const customerFirstNameSchema = z.string().trim()
  .min(1, "First name is required.")
  .max(32, "First name must be 32 characters or fewer.");

export const customerSurnameSchema = z.string().trim()
  .min(1, "Surname is required.")
  .max(32, "Surname must be 32 characters or fewer.");

export const customerEmailSchema = z.string().trim()
  .email("Enter a valid email address.")
  .max(256, "Email address must be 256 characters or fewer.");

export const customerContactNumberSchema = z.string().trim()
  .min(1, "Contact number is required.")
  .max(32, "Contact number must be 32 characters or fewer.");

export const customerHomeAddressSchema = z.string().trim()
  .min(3, "Home address must be at least 3 characters.")
  .max(512, "Home address must be 512 characters or fewer.");
