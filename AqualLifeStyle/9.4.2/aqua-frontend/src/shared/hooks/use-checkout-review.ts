"use client";

import { useCallback, useEffect, useSyncExternalStore } from "react";

type Account = { id: number; tenantId?: number | null } | null | undefined;
export type JoiningCheckoutStage = "unpaid" | "part-paid";
type Review = JoiningCheckoutStage | "unavailable" | "review" | null;
const eventName = "aqua:checkout-review";
const serverSnapshot = (): Review => "unavailable";

/** Authoritative payment state the caller already read from the server.
 * A reminder is retired only when this evidence proves the guarded checkout
 * settled; an uncertain outcome is never cleared by a refresh alone.
 */
export type CheckoutReviewEvidence = {
  firstInstalmentConfirmed?: boolean;
  joiningFullyPaid?: boolean;
};

const readMarker = (key: string): Review => {
  const value = window.localStorage.getItem(key);
  return value === "unpaid" || value === "part-paid"
    ? value : value === null ? null : "review";
};

/** A browser safety reminder, never payment evidence or a distributed lock.
 * No token, payment reference, checkout URL, name, email or amount is stored.
 * Only a verified server payment advance can open the next instalment stage,
 * and only server evidence of a settled checkout retires the reminder.
 */
export const useCheckoutReview = (
  account: Account,
  evidence: CheckoutReviewEvidence = {},
) => {
  const instalmentConfirmed = evidence.firstInstalmentConfirmed === true;
  const fullyPaid = evidence.joiningFullyPaid === true;
  const key = account && Number.isSafeInteger(account.id) && account.id > 0 &&
    typeof account.tenantId === "number" && Number.isSafeInteger(account.tenantId) && account.tenantId > 0
    ? `aqua.aqgreenCheckoutReview.${account.tenantId}.${account.id}` : null;
  const snapshot = useCallback((): Review => {
    if (!key || typeof window === "undefined") return "unavailable";
    try {
      const marker = readMarker(key);
      if (marker !== null && (fullyPaid || (marker === "unpaid" && instalmentConfirmed)))
        return null;
      return marker;
    } catch { return "unavailable"; }
  }, [key, instalmentConfirmed, fullyPaid]);
  useEffect(() => {
    if (!key || typeof window === "undefined" || (!instalmentConfirmed && !fullyPaid))
      return;
    try {
      const value = window.localStorage.getItem(key);
      if (value !== null &&
        (value === "unpaid" ? instalmentConfirmed || fullyPaid : fullyPaid)) {
        window.localStorage.removeItem(key);
        window.dispatchEvent(new Event(eventName));
      }
    } catch { /* storage unavailable: nothing to retire */ }
  }, [key, instalmentConfirmed, fullyPaid]);
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
