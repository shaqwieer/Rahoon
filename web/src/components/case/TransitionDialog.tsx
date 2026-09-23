"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, Dialog, Textarea, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { AvailableAction } from "@/lib/api/lender";
import { StepUpDialog } from "./StepUpDialog";

/**
 * Review dialog for a manual case transition: states what happens, collects the reason when required,
 * sends the expected status (stale-view protection) and an idempotency key, and surfaces guard reasons.
 */
export function TransitionDialog({ reference, action, expectedStatus, open, onClose }: {
  reference: string;
  action: AvailableAction | null;
  expectedStatus: string;
  open: boolean;
  onClose: () => void;
}) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; reasons: string[] } | null>(null);
  const [stepUp, setStepUp] = useState(false);

  if (!action) return null;

  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ label: string }>("POST", `/cases/${reference}/transitions`, { action: action.key, reason: reason.trim() || null, expectedStatus }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: `تم: ${action.labelAr} · الحالة الآن «${res.label}»` });
      setReason("");
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else setError(isApiError(e) ? { title: e.title, reasons: e.reasons ?? [] } : { title: "تعذّر تنفيذ الإجراء.", reasons: [] });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Dialog
        open={open && !stepUp}
        onClose={onClose}
        title={`${action.labelAr}${action.requiresReason ? "…" : ""}`}
        description="إجراء يُسجَّل في سجل الحالة مع الفاعل والوقت والسبب."
        footer={
          <>
            <Button variant="secondary" onClick={onClose}>رجوع للحالة</Button>
            <Button onClick={() => void run()} loading={busy} disabled={!action.enabled || (action.requiresReason && reason.trim().length < 5)}>
              تأكيد
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4 p-5">
          {!action.enabled ? (
            <Alert tone="warn" title="غير مؤهل بعد" body={<ul className="m-0 ps-5">{action.reasons.map((r) => <li key={r}>{r}</li>)}</ul>} />
          ) : null}
          {error ? (
            <Alert tone="err" title={error.title} body={error.reasons.length ? <ul className="m-0 ps-5">{error.reasons.map((r) => <li key={r}>{r}</li>)}</ul> : undefined} />
          ) : null}
          {action.requiresReason ? (
            <Textarea label="السبب" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} help="يظهر في سجل التدقيق." />
          ) : null}
          {action.requiresStepUp ? <p className="m-0 text-13 text-muted">سيُطلب رمز التحقق لتأكيد هذا الإجراء.</p> : null}
        </div>
      </Dialog>
      <StepUpDialog open={stepUp} onClose={() => setStepUp(false)} onVerified={() => { setStepUp(false); void run(); }} />
    </>
  );
}
