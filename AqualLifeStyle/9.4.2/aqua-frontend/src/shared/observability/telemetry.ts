import { publicEnv } from "@/src/shared/config";

type TelemetryEvent = {
  kind: "error" | "web-vital";
  name: string;
  value?: number;
  context?: string;
  id?: string;
  rating?: string;
};

const send = (event: TelemetryEvent) => {
  const endpoint = publicEnv.NEXT_PUBLIC_MONITORING_ENDPOINT;
  if (!endpoint || typeof navigator === "undefined") return;

  const body = JSON.stringify({
    ...event,
    path: typeof window === "undefined" ? undefined : window.location.pathname,
    timestamp: new Date().toISOString(),
  });

  if (navigator.sendBeacon?.(endpoint, body)) return;

  void fetch(endpoint, {
    body,
    headers: { "content-type": "application/json" },
    keepalive: true,
    method: "POST",
  }).catch(() => undefined);
};

export const reportApplicationError = (
  error: Error & { digest?: string },
  context: string,
) => {
  console.error(`[${context}]`, error);
  send({
    context,
    id: error.digest,
    kind: "error",
    name: error.name || "Error",
  });
};

export const reportWebVital = (metric: {
  id: string;
  name: string;
  rating: string;
  value: number;
}) => {
  send({
    id: metric.id,
    kind: "web-vital",
    name: metric.name,
    rating: metric.rating,
    value: metric.value,
  });
};
