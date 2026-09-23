"use client";

import { useRef, useState, type FormEvent } from "react";
import { ChoiceCard } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, AmountField, Button, DateText, Icon, Select, Textarea } from "@/components/ui";
import { apiSend } from "@/lib/api/client";

const DAYS = Array.from({ length: 28 }, (_, i) => ({ value: String(i + 1), label: String(i + 1) }));

/**
 * D08 — counter-proposal (a request, not an agreement). Each ticked change reveals its field; the submit is
 * enabled only when at least one ticked change has a value.
 */
export function CounterForm({ offerId, months }: { offerId: string; months: Array<{ value: string; label: string }> }) {
  const c = useOwnerCopy();
  const K = c.counter;
  const [change, setChange] = useState({ day: false, start: false, amount: false });
  const [day, setDay] = useState("");
  const [month, setMonth] = useState("");
  const [amount, setAmount] = useState<number | null>(null);
  const [reason, setReason] = useState("");
  const [done, setDone] = useState<{ message: string; responseDueOn: string } | null>(null);
  const { run, busy, error, resetKey } = useSubmit();
  const doneRef = useRef<HTMLDivElement>(null);

  const dayValue = change.day && day ? Number(day) : null;
  const monthValue = change.start && month ? month : null;
  const amountValue = change.amount && amount !== null && amount > 0 ? amount : null;
  const ready = dayValue !== null || monthValue !== null || amountValue !== null;
  const edit = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    resetKey();
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!ready) return;
    const res = await run((k) =>
      apiSend<{ responseDueOn: string; message: string }>(
        "POST",
        `/owner/offers/${offerId}/counter`,
        { installmentDay: dayValue, firstMonth: monthValue, installmentAmount: amountValue, reason: reason.trim() || null },
        { idempotencyKey: k },
      ),
    );
    if (res.ok) {
      setDone(res.data);
      requestAnimationFrame(() => doneRef.current?.focus());
    }
  };

  if (done) {
    return (
      <div ref={doneRef} tabIndex={-1} role="status" className="flex flex-col gap-3 rounded-[12px] border border-ok-line bg-ok-bg p-4 outline-none">
        <span className="flex items-center gap-2 text-18 font-bold text-ok">
          <Icon name="check_circle" size={26} />
          {K.doneTitle}
        </span>
        <ServerText as="p" text={done.message} className="m-0 text-17 leading-7" />
        {done.responseDueOn ? (
          <p className="m-0 text-15">
            {K.replyBy} <DateText value={done.responseDueOn} />
          </p>
        ) : null}
        <Button href="/owner" size="lg" variant="secondary" className="self-start">
          {c.backHome}
        </Button>
      </div>
    );
  }

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-4">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-2 p-0 text-16 font-semibold">{K.legend}</legend>
        <ChoiceCard type="checkbox" label={K.day} checked={change.day} onChange={(e) => edit(setChange)({ ...change, day: e.target.checked })} />
        <ChoiceCard type="checkbox" label={K.start} checked={change.start} onChange={(e) => edit(setChange)({ ...change, start: e.target.checked })} />
        <ChoiceCard type="checkbox" label={K.amount} checked={change.amount} onChange={(e) => edit(setChange)({ ...change, amount: e.target.checked })} />
      </fieldset>
      {change.day || change.start ? (
        <div className="grid grid-cols-2 gap-2.5">
          {change.day ? (
            <Select label={K.dayLabel} size="xl" options={DAYS} placeholder={K.choose} value={day} onChange={(e) => edit(setDay)(e.target.value)} containerClassName="col-span-1" />
          ) : null}
          {change.start ? (
            <Select label={K.startLabel} size="xl" options={months} placeholder={K.choose} value={month} onChange={(e) => edit(setMonth)(e.target.value)} containerClassName="col-span-1" />
          ) : null}
        </div>
      ) : null}
      {change.amount ? <AmountField label={K.amountLabel} size="xl" value={amount} onValueChange={edit(setAmount)} /> : null}
      <Textarea label={K.reason} optionalMark value={reason} maxLength={500} onChange={(e) => edit(setReason)(e.target.value)} className="text-16 leading-[26px]" />
      <div aria-live="assertive">{error ? <Alert tone="err" role="none">{error}</Alert> : null}</div>
      <div className="flex flex-col gap-1.5">
        <Button type="submit" size="xl" fullWidth softDisabled={!ready} loading={busy} loadingLabel={c.sending} aria-describedby="counter-hint" className="text-17">
          {K.submit}
        </Button>
        <span id="counter-hint" className="text-14 text-muted">
          {ready ? K.note : K.needChange}
        </span>
      </div>
    </form>
  );
}
