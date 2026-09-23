"use client";

import { useState, type ReactNode } from "react";
import { Alert, Button, Dialog, Textarea } from "@/components/ui";
import type { ButtonVariant } from "@/components/ui";
import { isApiError, useIdempotencyKey } from "@/lib/api/client";
import { StepUpDialog } from "./StepUpDialog";

export interface ReasonDialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: ReactNode;
  /** Field label (also the accessible name of the textarea). */
  label: string;
  placeholder?: string;
  help?: ReactNode;
  /** Content shown above the field (what happens, effects). */
  children?: ReactNode;
  confirmLabel: string;
  confirmVariant?: ButtonVariant;
  /** Minimum trimmed length before the confirm button enables (0 = optional text). */
  minLength?: number;
  maxLength?: number;
  initialText?: string;
  /**
   * Performs the mutation. Receives one idempotency key per logical submit: the same key is reused only
   * when the previous attempt never reached the API (network), otherwise a fresh key is issued.
   */
  onSubmit: (text: string, idempotencyKey: string) => Promise<void>;
}

/**
 * Reason / note dialog for case actions (decline, clarification, legal review, payment rejection …).
 * Surfaces the API title, guard `reasons` and field errors; a 403 `step_up_required` opens the OTP step-up
 * and retries once verified.
 */
export function ReasonDialog({
  open,
  onClose,
  title,
  description,
  label,
  placeholder,
  help,
  children,
  confirmLabel,
  confirmVariant = "primary",
  minLength = 5,
  maxLength = 1000,
  initialText = "",
  onSubmit,
}: ReasonDialogProps) {
  const key = useIdempotencyKey();
  const [text, setText] = useState(initialText);
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
      await onSubmit(text.trim(), key.get());
      key.reset();
      setText(initialText);
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

  const tooShort = text.trim().length < minLength;

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
            <Button variant={confirmVariant} onClick={() => void run()} loading={busy} disabled={tooShort}>
              {confirmLabel}
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4 p-5">
          {children}
          {error ? (
            <Alert
              tone="err"
              title={error.title}
              body={error.items.length ? <ul className="m-0 ps-5">{error.items.map((r) => <li key={r}>{r}</li>)}</ul> : undefined}
            />
          ) : null}
          <Textarea
            label={label}
            requiredMark={minLength > 0}
            optionalMark={minLength === 0}
            value={text}
            onChange={(e) => {
              setText(e.target.value);
              key.reset();
            }}
            maxLength={maxLength}
            placeholder={placeholder}
            help={help}
          />
        </div>
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
