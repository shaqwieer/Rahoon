"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { SystemState } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";

/** Opens the existing draft or creates the next version (copied from the latest) and goes to the builder. */
export default function NewSolutionPage() {
  const { ref } = useParams<{ ref: string }>();
  const router = useRouter();
  const key = useIdempotencyKey();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    apiSend<{ version: number }>("POST", `/cases/${ref}/solutions`, {}, { idempotencyKey: key.get() })
      .then((r) => !cancelled && router.replace(`/cases/${ref}/solutions/${r.version}`))
      .catch((e) => !cancelled && setError(isApiError(e) ? e.title : "تعذّر إنشاء إصدار جديد."));
    return () => {
      cancelled = true;
    };
  }, [ref, key, router]);

  return error ? (
    <SystemState kind="error" title="تعذّر فتح منشئ الحل" body={error} action={{ label: "العودة للحلول", href: `/cases/${ref}/solutions` }} />
  ) : (
    <SystemState kind="loading" title="جارٍ تجهيز منشئ الحل…" />
  );
}
