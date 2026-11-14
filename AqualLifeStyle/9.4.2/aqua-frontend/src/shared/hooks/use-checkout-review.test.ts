import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useCheckoutReview } from "./use-checkout-review";

describe("checkout review reminder", () => {
  beforeEach(() => window.localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  it("records before an attempt, survives remount, and cannot clear a same-stage uncertainty", () => {
    const first = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    expect(first.result.current.blockedFor("unpaid")).toBe(false);
    act(() => { expect(first.result.current.start("unpaid")).toBe(true); });
    expect(first.result.current.blockedFor("unpaid")).toBe(true);
    expect(first.result.current.start("unpaid")).toBe(false);
    first.unmount();
    const resumed = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    expect(resumed.result.current.blockedFor("unpaid")).toBe(true);
  });

  it("permits the next stage only when the caller has authoritative first-instalment advancement", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    const { result } = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    expect(result.current.blockedFor("unpaid")).toBe(true);
    expect(result.current.blockedFor("part-paid")).toBe(false);
    expect(result.current.advancedTo("part-paid")).toBe(true);
    act(() => { expect(result.current.start("part-paid")).toBe(true); });
    expect(result.current.blockedFor("part-paid")).toBe(true);
    expect(result.current.blockedFor("unpaid")).toBe(true);
  });

  it("does not leak reminders across Tenant or account scope", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    expect(renderHook(() => useCheckoutReview({ id: 8, tenantId: 1 })).result.current.blockedFor("unpaid")).toBe(false);
    expect(renderHook(() => useCheckoutReview({ id: 7, tenantId: 2 })).result.current.blockedFor("unpaid")).toBe(false);
    expect(renderHook(() => useCheckoutReview({ id: 7 })).result.current.blockedFor("unpaid")).toBe(true);
  });

  it("fails closed for corrupt reminders, unavailable storage, or an interrupted browser", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unknown-value");
    const corrupt = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    expect(corrupt.result.current.blockedFor("part-paid")).toBe(true);
    expect(corrupt.result.current.start("unpaid")).toBe(false);
    corrupt.unmount();
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("storage unavailable"); });
    const unavailable = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    expect(unavailable.result.current.blockedFor("unpaid")).toBe(true);
    expect(unavailable.result.current.start("unpaid")).toBe(false);
  });

  it("observes another tab's reminder without treating it as provider evidence", () => {
    const { result } = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }));
    act(() => {
      window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
      window.dispatchEvent(new StorageEvent("storage", { key: "aqua.aqgreenCheckoutReview.1.7" }));
    });
    expect(result.current.blockedFor("unpaid")).toBe(true);
  });

  it("retires the first-stage reminder only on authoritative first-instalment settlement", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    const retained = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }, { firstInstalmentConfirmed: false }));
    expect(retained.result.current.blockedFor("unpaid")).toBe(true);
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBe("unpaid");
    retained.unmount();
    const settled = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }, { firstInstalmentConfirmed: true }));
    expect(settled.result.current.blockedFor("unpaid")).toBe(false);
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBeNull();
  });

  it("keeps the second-stage reminder while only the first instalment is confirmed", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "part-paid");
    const { result } = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }, { firstInstalmentConfirmed: true }));
    expect(result.current.blockedFor("part-paid")).toBe(true);
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBe("part-paid");
  });

  it("retires every reminder once the joining total is confirmed, including a corrupt value", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unknown-value");
    const { result } = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }, { joiningFullyPaid: true }));
    expect(result.current.blockedFor("part-paid")).toBe(false);
    expect(result.current.blockedFor("unpaid")).toBe(false);
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBeNull();
  });

  it("does not treat a missing joining payment as settled evidence", () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "part-paid");
    const { result } = renderHook(() => useCheckoutReview({ id: 7, tenantId: 1 }, { joiningFullyPaid: false }));
    expect(result.current.blockedFor("part-paid")).toBe(true);
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBe("part-paid");
  });
});
