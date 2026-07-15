"use client";

import { useReportWebVitals } from "next/web-vitals";

import { reportWebVital } from "./telemetry";

export const WebVitals = () => {
  useReportWebVitals(reportWebVital);
  return null;
};
