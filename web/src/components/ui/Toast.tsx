"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";

export type ToastTone = "ok" | "info" | "err" | "offline";

export interface ToastOptions {
  message: ReactNode;
  tone?: ToastTone;
  /** Optional non-critical action, e.g. «تراجع». Toasts never carry critical actions (C05). */
  action?: { label: string; onClick: () => void };
  /** Milliseconds before auto-dismiss (default 6000). Paused while hovered or focused. */
  duration?: number;
}

interface ToastItem extends ToastOptions {
  id: number;
}

interface ToastApi {
  toast: (options: ToastOptions) => number;
  dismiss: (id: number) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

const TONE_ICON: Record<ToastTone, { icon: string; color: string }> = {
  ok: { icon: "check_circle", color: "#9CCBB0" },
  info: { icon: "info", color: "#9DC0DE" },
  err: { icon: "error", color: "#EFA59C" },
  offline: { icon: "wifi_off", color: "#EFA59C" },
};

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => setItems((list) => list.filter((t) => t.id !== id)), []);
  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setItems((list) => [...list.slice(-2), { ...options, id }]);
    return id;
  }, []);
  const api = useMemo(() => ({ toast, dismiss }), [toast, dismiss]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <ToastRegion items={items} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used inside <ToastProvider>");
  return ctx;
}

function ToastRegion({ items, onDismiss }: { items: ToastItem[]; onDismiss: (id: number) => void }) {
  const { t } = useI18n();
  return (
    <section
      aria-label={t.toast.region}
      className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-center gap-2 md:inset-x-auto md:end-6 md:bottom-6 md:items-end"
    >
      {/* Always-present live region so additions are announced. */}
      <div role="status" aria-live="polite" className="flex flex-col gap-2">
        {items.map((item) => (
          <ToastCard key={item.id} item={item} onDismiss={onDismiss} />
        ))}
      </div>
    </section>
  );
}

function ToastCard({ item, onDismiss }: { item: ToastItem; onDismiss: (id: number) => void }) {
  const { t } = useI18n();
  const [paused, setPaused] = useState(false);
  const remaining = useRef(item.duration ?? 6000);
  const startedAt = useRef(0);

  useEffect(() => {
    if (paused) return;
    startedAt.current = Date.now();
    const timer = window.setTimeout(() => onDismiss(item.id), remaining.current);
    return () => {
      window.clearTimeout(timer);
      remaining.current = Math.max(0, remaining.current - (Date.now() - startedAt.current));
    };
  }, [paused, item.id, onDismiss]);

  const tone = TONE_ICON[item.tone ?? "ok"];
  return (
    <div
      className="surface-dark pointer-events-auto flex w-[min(100%,360px)] min-w-[min(320px,100%)] items-center gap-3 rounded-md bg-ink px-4 py-3 text-white shadow-3"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={() => setPaused(false)}
    >
      <Icon name={tone.icon} size={20} style={{ color: tone.color }} />
      <span className="flex-1 text-14">{item.message}</span>
      {item.action ? (
        <button
          type="button"
          className="min-h-9 bg-transparent text-14 font-semibold text-white underline underline-offset-[3px]"
          onClick={() => {
            item.action?.onClick();
            onDismiss(item.id);
          }}
        >
          {item.action.label}
        </button>
      ) : null}
      <button
        type="button"
        aria-label={t.common.close}
        className="inline-flex size-9 items-center justify-center rounded-sm text-inv-2 hover:bg-inv-raised hover:text-white"
        onClick={() => onDismiss(item.id)}
      >
        <Icon name="close" size={18} />
      </button>
    </div>
  );
}
