"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useTeamCopy } from "@/components/team/TeamShell";
import { apiSend, apiUpload, isApiError, useIdempotencyKey, type HttpMethod } from "@/lib/api/client";

/** Posts a team action with one idempotency key per logical submit, then refreshes the server data. */
export function useTeamAction() {
  const c = useTeamCopy();
  const router = useRouter();
  const key = useIdempotencyKey();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  async function run(method: HttpMethod, path: string, body?: unknown, form?: FormData): Promise<boolean> {
    setBusy(true);
    setError(null);
    setFieldErrors({});
    try {
      if (form) await apiUpload(path, form, { idempotencyKey: key.get() });
      else await apiSend(method, path, body, { idempotencyKey: key.get() });
      key.reset();
      router.refresh();
      return true;
    } catch (e) {
      if (!(isApiError(e) && (e.code === "network" || e.code === "offline"))) key.reset();
      if (isApiError(e)) {
        if (e.code === "network" || e.code === "offline") setError(c.networkError);
        else {
          const fe: Record<string, string> = {};
          for (const [k, v] of Object.entries(e.errors ?? {})) if (v[0]) fe[k] = v[0];
          setFieldErrors(fe);
          setError([e.title, ...(e.reasons ?? [])].filter(Boolean).join(" — ") || c.genericError);
        }
      } else setError(c.genericError);
      return false;
    } finally {
      setBusy(false);
    }
  }

  return { run, busy, error, fieldErrors, clear: () => { setError(null); setFieldErrors({}); } };
}
