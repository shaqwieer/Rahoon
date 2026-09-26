"use client";

import { useState, type FormEvent } from "react";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { ChoiceCard } from "@/components/owner/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend } from "@/lib/api/client";

const SUBJECTS_OBJECTION = ["data", "amount", "decision", "other"];
const SUBJECTS_COMPLAINT = ["service", "other"];

export function RaiseConcern({ reference, kind, initialSubject }: { reference: string; kind: "objection" | "complaint"; initialSubject: string | null }) {
  const c = useRequestCopy();
  const K = c.concern;
  const ref = encodeURIComponent(reference);
  const subjects = kind === "complaint" ? SUBJECTS_COMPLAINT : SUBJECTS_OBJECTION;
  const [subject, setSubject] = useState<string>(initialSubject && subjects.includes(initialSubject) ? initialSubject : subjects[0]);
  const [text, setText] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [sent, setSent] = useState<{ reference: string; message: string } | null>(null);
  const send = useRequestSubmit();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) {
      setFieldError(K.textError);
      return;
    }
    const res = await send.run((key) => apiSend<{ reference: string; message: string }>("POST", `/my/requests/${ref}/concerns`, { kind, subject, text: text.trim() }, { idempotencyKey: key }));
    if (res.ok) setSent(res.data);
  };

  const title = kind === "complaint" ? K.titleComplaint : K.titleObjection;
  if (sent) {
    return (
      <IndividualFrame title={title} sub={<bdi dir="ltr">{reference}</bdi>} back={{ href: `/my/requests/${ref}` }}>
        <span className="flex size-14 items-center justify-center rounded-full bg-ok-bg text-ok">
          <Icon name="task_alt" size={30} />
        </span>
        <h1 className="m-0 text-24 leading-9 font-bold">{K.sentTitle}</h1>
        <p className="m-0 text-17 leading-7">{sent.message}</p>
        <Button href={`/my/requests/${ref}`} size="xl" fullWidth>
          {K.back}
        </Button>
      </IndividualFrame>
    );
  }

  return (
    <IndividualFrame title={title} sub={<bdi dir="ltr">{reference}</bdi>} back={{ href: `/my/requests/${ref}` }}>
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <h1 className="m-0 text-24 leading-9 font-bold">{kind === "complaint" ? K.headingComplaint : K.headingObjection}</h1>
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-2 text-16 font-semibold">{K.subject}</legend>
          {subjects.map((s) => (
            <ChoiceCard key={s} type="radio" name="subject" checked={subject === s} onChange={() => setSubject(s)} label={K.subjects[s]} />
          ))}
        </fieldset>
        <Textarea
          label={K.text}
          rows={5}
          maxLength={2000}
          value={text}
          onChange={(e) => {
            setText(e.target.value);
            setFieldError(null);
          }}
          error={fieldError ?? undefined}
        />
        {kind === "complaint" ? <p className="m-0 text-14 text-muted">{K.complaintNote}</p> : null}
        {send.error ? <Alert tone="err">{send.error}</Alert> : null}
        <Button type="submit" size="xl" fullWidth loading={send.busy}>
          {K.send}
        </Button>
      </form>
    </IndividualFrame>
  );
}
