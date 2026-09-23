"use client";

import { useState } from "react";
import { isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { OwnerCopy } from "./copy";
import { useOwnerCopy } from "./values";

/** Human message for a failed owner request (network/offline copy, else the server's Arabic title). */
export function errorText(e: unknown, c: OwnerCopy): string {
  if (!isApiError(e)) return c.genericError;
  if (e.code === "network") return c.networkError;
  if (e.code === "offline") return c.offlineError;
  if (e.code === "unavailable") return c.genericError;
  const field = e.errors ? Object.values(e.errors)[0]?.[0] : undefined;
  return field ?? (e.title || c.genericError);
}

/**
 * One logical submit = one idempotency key. The key survives only a `network`/`offline` failure (so a retry
 * replays instead of acting twice); any API answer or success resets it.
 */
export function useSubmit() {
  const key = useIdempotencyKey();
  const c = useOwnerCopy();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /** `onError` may return a message, `null` (handled — show nothing) or `undefined` (default message). */
  async function run<T>(fn: (idempotencyKey: string) => Promise<T>, onError?: (e: unknown) => string | null | undefined): Promise<{ ok: true; data: T } | { ok: false; error: unknown }> {
    setBusy(true);
    setError(null);
    try {
      const data = await fn(key.get());
      key.reset();
      return { ok: true, data };
    } catch (e) {
      if (!(isApiError(e) && (e.code === "network" || e.code === "offline"))) key.reset();
      const custom = onError?.(e);
      setError(custom === undefined ? errorText(e, c) : custom);
      return { ok: false, error: e };
    } finally {
      setBusy(false);
    }
  }

  return { run, busy, error, setError, resetKey: key.reset };
}
