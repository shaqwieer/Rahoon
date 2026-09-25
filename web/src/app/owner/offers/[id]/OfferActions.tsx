"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ownerCopy } from "@/components/owner/copy";
import { useSubmit } from "@/components/owner/useSubmit";
import { useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, Dialog, Textarea } from "@/components/ui";
import { apiSend } from "@/lib/api/client";

/** D07 «لا يناسبني»: a gentle dialog with an optional reason. Declining never triggers any adverse action. */
export function DeclineButton({ offerId }: { offerId: string }) {
  const c = useOwnerCopy();
  const O = c.offer;
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const { run, busy, error, setError, resetKey } = useSubmit();

  const send = async () => {
    const res = await run((k) => apiSend<{ message: string }>("POST", `/owner/offers/${offerId}/decline`, { reason: reason.trim() || null }, { idempotencyKey: k }));
    if (res.ok) {
      setOpen(false);
      router.refresh();
    }
  };

  return (
    <>
      <Button variant="secondary" size="lg" className="min-h-[50px] rounded-[8px] text-15" onClick={() => setOpen(true)}>
        {O.decline}
      </Button>
      <Dialog
        open={open}
        onClose={() => {
          setOpen(false);
          setError(null);
        }}
        title={O.declineTitle}
        footer={
          <>
            <Button size="lg" loading={busy} loadingLabel={c.sending} onClick={() => void send()}>
              {O.declineSend}
            </Button>
            <Button size="lg" variant="secondary" onClick={() => setOpen(false)}>
              {O.declineCancel}
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4 p-5">
          <p className="m-0 text-16 leading-[26px]">{O.declineBody}</p>
          <Textarea
            label={O.declineReason}
            optionalMark
            value={reason}
            maxLength={500}
            onChange={(e) => {
              setReason(e.target.value);
              resetKey();
            }}
            className="text-16"
          />
          <div aria-live="assertive">{error ? <Alert tone="err" role="none">{error}</Alert> : null}</div>
        </div>
      </Dialog>
    </>
  );
}

/** Expired offer → «طلب عرض جديد»: a portal message to the case manager (always in Arabic for the case team). */
export function RequestNewOffer({ version }: { version: number }) {
  const c = useOwnerCopy();
  const { run, busy, error } = useSubmit();
  const [sent, setSent] = useState(false);
  const ask = async () => {
    const res = await run((k) => apiSend<{ sent: boolean }>("POST", "/owner/messages", { body: ownerCopy("ar").offer.requestNewBody(version) }, { idempotencyKey: k }));
    if (res.ok) setSent(true);
  };
  return (
    <div aria-live="polite" className="flex flex-col gap-2">
      {sent ? (
        <Alert tone="ok" role="none">
          {c.offer.requestNewSent}
        </Alert>
      ) : (
        <>
          <Button size="xl" fullWidth loading={busy} loadingLabel={c.sending} onClick={() => void ask()} className="text-17">
            {c.offer.requestNew}
          </Button>
          {error ? <Alert tone="err" role="none">{error}</Alert> : null}
        </>
      )}
    </div>
  );
}
