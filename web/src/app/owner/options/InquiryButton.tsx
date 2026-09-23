"use client";

import { useState } from "react";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText } from "@/components/owner/values";
import { Alert, Button } from "@/components/ui";
import { apiSend } from "@/lib/api/client";

/** D06 — «اسأل عن هذا الخيار»: an inquiry (message + task for the case manager), never a commitment. */
export function InquiryButton({ optionKey, label, describedBy }: { optionKey: string; label: string; describedBy?: string }) {
  const { run, busy, error } = useSubmit();
  const [sent, setSent] = useState<string | null>(null);
  const ask = async () => {
    const res = await run((k) => apiSend<{ sent: boolean; message: string }>("POST", `/owner/options/${encodeURIComponent(optionKey)}/inquiry`, undefined, { idempotencyKey: k }));
    if (res.ok) setSent(res.data.message);
  };
  return (
    <div aria-live="polite" className="flex flex-col gap-2">
      {sent ? (
        <Alert tone="ok" role="none" compact>
          <ServerText text={sent} />
        </Alert>
      ) : (
        <>
          <Button variant="text" size="lg" className="self-start px-0 text-15" loading={busy} onClick={() => void ask()} aria-describedby={describedBy}>
            <ServerText text={label} />
          </Button>
          {error ? (
            <Alert tone="err" role="none" compact>
              {error}
            </Alert>
          ) : null}
        </>
      )}
    </div>
  );
}
