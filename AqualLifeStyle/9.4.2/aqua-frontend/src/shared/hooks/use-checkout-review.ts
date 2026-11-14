"use client";

import { useCallback, useSyncExternalStore } from "react";

type Account = { id: number; tenantId?: number | null } | null | undefined;
export type JoiningCheckoutStage = "unpaid" | "part-paid";
type Review = JoiningCheckoutStage | "unavailable" | "review" | null;
const eventName = "aqua:checkout-review";
const serverSnapshot = (): Review => "unavailable";

/** A browser safety reminder, never payment evidence or a distributed lock.
 * No token, payment reference, checkout URL, name, email or amount is stored.
 * Only a verified server payment advance can open the next instalment stage.
 */
export const useCheckoutReview = (account: Account) => {
  const key = account && Number.isSafeInteger(account.id) && account.id > 0 &&
    typeof account.tenantId === "number" && Number.isSafeInteger(account.tenantId) && account.tenantId > 0
    ? `aqua.aqgreenCheckoutReview.${account.tenantId}.${account.id}` : null;
  const snapshot = useCallback((): Review => {
    if (!key || typeof window === "undefined") return "unavailable";
    try {
      const value = window.localStorage.getItem(key);
      return value === null || value === "unpaid" || value === "part-paid" ? value : "review";
    } catch { return "unavailable"; }
  }, [key]);
  const subscribe = useCallback((changed: () => void) => {
    window.addEventListener("storage", changed);
    window.addEventListener(eventName, changed);
    return () => {
      window.removeEventListener("storage", changed);
      window.removeEventListener(eventName, changed);
    };
  }, []);
  const review = useSyncExternalStore(subscribe, snapshot, serverSnapshot);
  const blocked = (value: Review, stage?: JoiningCheckoutStage) => value !== null &&
    !(value === "unpaid" && stage === "part-paid");
  const start = (stage: JoiningCheckoutStage) => {
    if (!key || blocked(snapshot(), stage)) return false;
    try {
      window.localStorage.setItem(key, stage);
      window.dispatchEvent(new Event(eventName));
      return true;
    } catch { return false; }
  };
  return {
    blockedFor: (stage?: JoiningCheckoutStage) => blocked(review, stage),
    advancedTo: (stage?: JoiningCheckoutStage) => review === "unpaid" && stage === "part-paid",
    start,
  };
};
