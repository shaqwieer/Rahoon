"use client";

import { useState, type ReactNode } from "react";
import { Alert, Button, Dialog } from "@/components/ui";
import type { ButtonVariant } from "@/components/ui";
import { isApiError, useIdempotencyKey } from "@/lib/api/client";
import { StepUpDialog } from "./StepUpDialog";

export interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: ReactNode;
  /** What will happen — shown before confirming (review-screen rule: effects are stated first). */
  children?: ReactNode;
  confirmLabel: string;
  confirmVariant?: ButtonVariant;
  /** Performs the mutation; see ReasonDialog for the idempotency-key contract. */
  onConfirm: (idempotencyKey: string) => Promise<void>;
}

/**
 * Confirmation for an action without free text (activate, create schedule, match payment, end sessions).
 * Guard refusals (422 `reasons`) are listed verbatim; 403 `step_up_required` opens the OTP step-up and retries.
 */
export function ConfirmDialog({ open, onClose, title, description, children, confirmLabel, confirmVariant = "primary", onConfirm }: ConfirmDialogProps) {
  const key = useIdempotencyKey();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; items: string[] } | null>(null);
  const [stepUp, setStepUp] = useState(false);

  const close = () => {
    setError(null);
    onClose();
  };

  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      await onConfirm(key.get());
      key.reset();
      onClose();
    } catch (e) {
      if (!isApiError(e) || e.status !== 0) key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else if (isApiError(e))
        setError({
          title: e.status === 0 ? "تعذّر الاتصال بالخادم. أعد المحاولة." : e.title || "تعذّر تنفيذ الإجراء.",
          items: [...(e.reasons ?? []), ...Object.values(e.errors ?? {}).flat()],
        });
      else setError({ title: "تعذّر تنفيذ الإجراء.", items: [] });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Dialog
        open={open && !stepUp}
        onClose={close}
        title={title}
        description={description}
        footer={
          <>
            <Button variant="secondary" onClick={close}>
              إلغاء
            </Button>
            <Button variant={confirmVariant} onClick={() => void run()} loading={busy}>
              {confirmLabel}
            </Button>
          </>
        }
      >
        {children || error ? (
          <div className="flex flex-col gap-4 p-5">
            {children}
            {error ? (
              <Alert
                tone="err"
                title={error.title}
                body={error.items.length ? <ul className="m-0 ps-5">{error.items.map((r) => <li key={r}>{r}</li>)}</ul> : undefined}
              />
            ) : null}
          </div>
        ) : null}
      </Dialog>
      <StepUpDialog
        open={stepUp}
        onClose={() => setStepUp(false)}
        onVerified={() => {
          setStepUp(false);
          void run();
        }}
      />
    </>
  );
}
