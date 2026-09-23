"use client";

import { useState } from "react";
import { Alert, Button, Dialog, OtpInput, SandboxCodeBox } from "@/components/ui";
import { apiSend, isApiError } from "@/lib/api/client";

/**
 * MFA step-up for sensitive decisions (approvals, cancellation, referral, closure):
 * sends an OTP, verifies it, and resolves so the caller can retry the action within 5 minutes.
 */
export function StepUpDialog({ open, onClose, onVerified }: { open: boolean; onClose: () => void; onVerified: () => void }) {
  const [sent, setSent] = useState<{ destination: string; sandboxCode?: string | null } | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const send = async () => {
    setBusy(true);
    setError(null);
    try {
      setSent(await apiSend<{ destination: string; sandboxCode?: string | null }>("POST", "/auth/step-up/start"));
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر إرسال الرمز.");
    } finally {
      setBusy(false);
    }
  };

  const verify = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", "/auth/step-up/verify", { code });
      setSent(null);
      setCode("");
      onVerified();
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر التحقق من الرمز.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="sm"
      title="تأكيد برمز التحقق"
      description="هذا الإجراء حساس ويتطلب إعادة إدخال رمز التحقق. يبقى التأكيد صالحاً 5 دقائق."
      footer={
        sent ? (
          <Button onClick={() => void verify()} loading={busy} disabled={code.length !== 6}>تحقق</Button>
        ) : (
          <Button onClick={() => void send()} loading={busy}>إرسال الرمز</Button>
        )
      }
    >
      <div className="flex flex-col gap-3">
        {error ? <Alert tone="err" title={error} /> : null}
        {sent ? (
          <>
            <p className="m-0 text-14">
              أرسلنا رمزاً من 6 أرقام إلى <bdi dir="ltr">{sent.destination}</bdi>.
            </p>
            {sent.sandboxCode ? <SandboxCodeBox code={sent.sandboxCode} title="بيئة تجريبية — الرسائل النصية محاكاة ولا تُرسل: الرمز" /> : null}
            <OtpInput value={code} onChange={setCode} label="رمز من 6 أرقام" error={!!error} autoFocus />
          </>
        ) : null}
      </div>
    </Dialog>
  );
}
