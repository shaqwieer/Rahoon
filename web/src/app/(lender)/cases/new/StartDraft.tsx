"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { SystemState } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";

/** Creates the draft once (idempotent across re-renders) and replaces the URL with the wizard. */
export function StartDraft() {
  const router = useRouter();
  const key = useIdempotencyKey();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    apiSend<{ reference: string }>("POST", "/cases/drafts", {}, { idempotencyKey: key.get() })
      .then((r) => {
        if (!cancelled) router.replace(`/cases/new/${r.reference}`);
      })
      .catch((e) => {
        if (!cancelled) setError(isApiError(e) ? e.title : "تعذّر بدء حالة جديدة.");
      });
    return () => {
      cancelled = true;
    };
  }, [key, router]);

  if (error) return <SystemState kind="error" title="تعذّر بدء حالة جديدة" body={error} />;
  return <SystemState kind="loading" title="جارٍ تجهيز حالة جديدة…" />;
}
