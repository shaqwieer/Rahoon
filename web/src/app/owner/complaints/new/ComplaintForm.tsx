"use client";

import { useRef, useState, type FormEvent } from "react";
import type { ComplaintSubmitted } from "@/components/owner/types";
import { ChoiceCard } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, DateText, Icon, Textarea } from "@/components/ui";
import { apiSend } from "@/lib/api/client";

type ComplaintType = "complaint" | "objection";

/** D13 — complaint or objection routed to an independent reviewer; success shows the reference and reply date. */
export function ComplaintForm({ initialType }: { initialType: ComplaintType }) {
  const c = useOwnerCopy();
  const K = c.complaint;
  const [type, setType] = useState<ComplaintType>(initialType);
  const [body, setBody] = useState("");
  const [bodyError, setBodyError] = useState<string | null>(null);
  const [done, setDone] = useState<ComplaintSubmitted | null>(null);
  const { run, busy, error, resetKey } = useSubmit();
  const bodyRef = useRef<HTMLTextAreaElement>(null);
  const doneRef = useRef<HTMLDivElement>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (body.trim().length < 10) {
      setBodyError(K.bodyRequired);
      bodyRef.current?.focus();
      return;
    }
    setBodyError(null);
    const res = await run((k) => apiSend<ComplaintSubmitted>("POST", "/owner/complaints", { type, body: body.trim(), subject: null }, { idempotencyKey: k }));
    if (res.ok) {
      setDone(res.data);
      requestAnimationFrame(() => doneRef.current?.focus());
    }
  };

  if (done) {
    return (
      <div ref={doneRef} tabIndex={-1} role="status" className="flex flex-col gap-3 outline-none">
        <div className="flex flex-col gap-3 rounded-[12px] border border-ok-line bg-ok-bg p-4">
          <span className="flex items-center gap-2 text-18 font-bold text-ok">
            <Icon name="check_circle" size={26} />
            {K.doneTitle}
          </span>
          <ServerText as="p" text={done.message} className="m-0 text-17 leading-7" />
        </div>
        <dl className="m-0 flex flex-col rounded-[12px] border border-line bg-white px-4 py-1">
          <div className="flex items-baseline justify-between gap-3 py-2.5">
            <dt className="text-muted">{K.reference}</dt>
            <dd className="m-0">
              <bdi dir="ltr" className="font-mono font-semibold">
                {done.reference}
              </bdi>
            </dd>
          </div>
          <div className="flex items-baseline justify-between gap-3 border-t border-divider py-2.5">
            <dt className="text-muted">{K.dueOn}</dt>
            <dd className="m-0 font-semibold">
              <DateText value={done.dueOn} mode="both" />
            </dd>
          </div>
        </dl>
        <Button href="/owner/complaints" variant="secondary" size="lg" className="min-h-[52px] rounded-[8px]">
          {K.track}
        </Button>
        <Button href="/owner" size="lg" className="min-h-[52px] rounded-[8px]">
          {c.backHome}
        </Button>
        <p className="m-0 text-13 leading-5 text-muted">{K.regulator}</p>
      </div>
    );
  }

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-3.5">
      <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-2 p-0 text-16 font-semibold">{K.legend}</legend>
        {(["complaint", "objection"] as const).map((v) => (
          <ChoiceCard
            key={v}
            type="radio"
            name="complaint-type"
            value={v}
            label={K[v]}
            checked={type === v}
            onChange={() => {
              setType(v);
              resetKey();
            }}
          />
        ))}
      </fieldset>
      <Textarea
        ref={bodyRef}
        label={K.body}
        requiredMark
        value={body}
        maxLength={4000}
        error={bodyError}
        onChange={(e) => {
          setBody(e.target.value);
          resetKey();
        }}
        className="min-h-[110px] text-16 leading-[26px]"
      />
      <div aria-live="assertive">{error ? <Alert tone="err" role="none">{error}</Alert> : null}</div>
      <Button type="submit" size="xl" fullWidth loading={busy} loadingLabel={c.sending} className="text-17">
        {K.submit}
      </Button>
      <p className="m-0 text-13 leading-5 text-muted">{K.regulator}</p>
    </form>
  );
}
