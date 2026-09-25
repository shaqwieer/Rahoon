"use client";

import { useRef, useState, type FormEvent } from "react";
import { ChoiceCard } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, Icon } from "@/components/ui";
import { apiSend } from "@/lib/api/client";

/** D11 — optional reason (single choice) + callback request. No reason is required to ask for a call. */
export function HardshipForm() {
  const c = useOwnerCopy();
  const H = c.help;
  const [reason, setReason] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const { run, busy, error } = useSubmit();
  const doneRef = useRef<HTMLDivElement>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const res = await run((k) => apiSend<{ message: string }>("POST", "/owner/hardship", { reasonKey: reason }, { idempotencyKey: k }));
    if (res.ok) {
      setDone(res.data.message);
      requestAnimationFrame(() => doneRef.current?.focus());
    }
  };

  if (done) {
    return (
      <div ref={doneRef} tabIndex={-1} role="status" className="flex flex-col gap-3 rounded-[12px] border border-ok-line bg-ok-bg p-4 outline-none">
        <span className="flex items-center gap-2 text-18 font-bold text-ok">
          <Icon name="check_circle" size={26} />
          {H.doneTitle}
        </span>
        <ServerText as="p" text={done} className="m-0 text-17 leading-7" />
      </div>
    );
  }

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-3.5">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0" aria-describedby="hardship-optional">
        <legend className="mb-2 p-0 text-16 font-semibold">{H.legend}</legend>
        <span id="hardship-optional" className="-mt-1 mb-1 text-14 text-muted">
          {H.optionalNote}
        </span>
        {H.reasons.map((r) => (
          <ChoiceCard key={r.key} type="radio" name="hardship-reason" value={r.key} icon={r.icon} label={r.label} checked={reason === r.key} onChange={() => setReason(r.key)} />
        ))}
      </fieldset>
      <div aria-live="assertive">{error ? <Alert tone="err">{error}</Alert> : null}</div>
      <Button type="submit" size="xl" fullWidth icon="call" loading={busy} loadingLabel={c.sending} className="text-17">
        {H.call}
      </Button>
      <p className="m-0 text-14 leading-[22px] text-muted">{H.privacy}</p>
    </form>
  );
}
