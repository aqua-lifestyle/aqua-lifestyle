"use client";

import { CheckCircle2, Info, X, XCircle } from "lucide-react";
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from "react";

import { cn } from "@/src/shared/lib/utils";

type ToastType = "error" | "info" | "success" | "warning";

type Toast = {
  duration?: number;
  id: string;
  message: string;
  title?: string;
  type: ToastType;
};

type ToastContextValue = {
  toast: (toast: Omit<Toast, "id">) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

const iconMap: Record<ToastType, typeof Info> = {
  error: XCircle,
  info: Info,
  success: CheckCircle2,
  warning: Info,
};

const styleMap: Record<ToastType, string> = {
  error: "bg-error text-error-foreground",
  info: "bg-info text-info-foreground",
  success: "bg-success text-success-foreground",
  warning: "bg-warning text-warning-foreground",
};

export const ToastProvider = ({ children }: { children: React.ReactNode }) => {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const removeToast = useCallback((id: string) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const toast = useCallback(
    (newToast: Omit<Toast, "id">) => {
      const id = `${Date.now()}-${Math.random()}`;
      const duration = newToast.duration ?? 5000;

      setToasts((current) => [
        ...current,
        { ...newToast, id, duration } as Toast,
      ]);

      setTimeout(() => {
        removeToast(id);
      }, duration);
    },
    [removeToast],
  );

  const value = useMemo(() => ({ toast }), [toast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="pointer-events-none fixed inset-x-4 bottom-4 z-[100] flex flex-col items-end gap-2 sm:left-auto">
        {toasts.map((toastItem) => {
          const Icon = iconMap[toastItem.type];

          return (
            <div
              key={toastItem.id}
              className={cn(
                "pointer-events-auto flex w-full max-w-80 items-start gap-3 rounded-xl p-4 shadow-lg animate-slide-in-right",
                styleMap[toastItem.type],
              )}
              role="alert"
            >
              <Icon aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
              <div className="min-w-0 flex-1 wrap-anywhere">
                {toastItem.title ? (
                  <p className="font-semibold">{toastItem.title}</p>
                ) : null}
                <p className="text-sm">{toastItem.message}</p>
              </div>
              <button
                aria-label="Dismiss notification"
                className="inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center rounded-lg opacity-80 transition hover:bg-white/20 hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-current"
                onClick={() => removeToast(toastItem.id)}
                type="button"
              >
                <X aria-hidden="true" className="size-4" />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
};

export const useToast = () => {
  const context = useContext(ToastContext);

  if (!context) {
    throw new Error("useToast must be used within a ToastProvider.");
  }

  return context;
};
