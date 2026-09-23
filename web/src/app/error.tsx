"use client";

import { SystemState } from "@/components/ui/SystemState";
import { useI18n } from "@/lib/i18n/client";

/** Route-level error boundary (C11 error state). The digest doubles as the ERR reference for support. */
export default function ErrorBoundary({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const { t } = useI18n();
  const ref = `ERR-${(error.digest ?? "0000").slice(0, 4).toUpperCase()}`;
  return (
    <main id="main" className="mx-auto w-full max-w-[640px] px-6 py-12">
      <SystemState kind="error" layout="page" headingLevel={1} errorRef={ref} action={{ label: t.sysStates.errorAction, onClick: reset }} />
    </main>
  );
}
