"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { apiSend, isApiError } from "@/lib/api/client";

export function WithdrawInterest({ reference }: { reference: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return (
    <div className="flex items-center gap-3">
      <Button variant="text" loading={busy} onClick={async () => {
        setBusy(true);
        try {
          await apiSend("POST", `/market/interests/${reference}/withdraw`);
          router.refresh();
        } catch (err) {
          setError(isApiError(err) ? err.title : "تعذّر السحب.");
        } finally {
          setBusy(false);
        }
      }}>سحب الاهتمام</Button>
      {error ? <span className="text-13 text-err">{error}</span> : null}
    </div>
  );
}
