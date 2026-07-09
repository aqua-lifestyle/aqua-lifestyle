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
  error: "bg-error text-white",
  info: "bg-info text-white",
  success: "bg-success text-white",
  warning: "bg-warning text-primary",
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
      <div className="fixed right-4 top-4 z-[100] flex flex-col gap-2">
        {toasts.map((toastItem) => {
          const Icon = iconMap[toastItem.type];

          return (
            <div
              key={toastItem.id}
              className={cn(
                "flex w-80 items-start gap-3 rounded-xl p-4 shadow-lg animate-slide-in-right",
                styleMap[toastItem.type],
              )}
              role="alert"
            >
              <Icon className="mt-0.5 size-5 shrink-0" />
              <div className="min-w-0 flex-1">
                {toastItem.title ? (
                  <p className="font-semibold">{toastItem.title}</p>
                ) : null}
                <p className="text-sm">{toastItem.message}</p>
              </div>
              <button
                aria-label="Dismiss notification"
                className="rounded-md p-1 opacity-80 transition hover:bg-white/20 hover:opacity-100"
                onClick={() => removeToast(toastItem.id)}
                type="button"
              >
                <X className="size-4" />
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
