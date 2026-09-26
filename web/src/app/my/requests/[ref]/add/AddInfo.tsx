"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { DocRow } from "../apply/RequestWizard";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import type { MyRequestDetail } from "@/lib/api/requests";

export function AddInfo({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const A = c.add;
  const router = useRouter();
  const back = `/my/requests/${encodeURIComponent(detail.reference)}`;
  const [text, setText] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const send = useRequestSubmit();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) {
      setFieldError(A.textError);
      return;
    }
    const res = await send.run((key) => apiSend("POST", `/my/requests/${encodeURIComponent(detail.reference)}/additions`, { text: text.trim() }, { idempotencyKey: key }));
    if (res.ok) router.push(back);
  };

  return (
    <IndividualFrame title={A.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: back }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{A.heading}</h1>
      <p className="m-0 text-16 leading-7 text-charcoal">{A.lead}</p>
      {detail.status === "info_requested" && detail.nextStepText ? <Alert tone="warn">{detail.nextStepText}</Alert> : null}
      <form noValidate onSubmit={submit} className="flex flex-col gap-3">
        <Textarea
          label={A.text}
          rows={5}
          maxLength={2000}
          value={text}
          onChange={(e) => {
            setText(e.target.value);
            setFieldError(null);
          }}
          error={fieldError ?? undefined}
        />
        {send.error ? <Alert tone="err">{send.error}</Alert> : null}
        <Button type="submit" size="xl" fullWidth loading={send.busy}>
          {A.send}
        </Button>
      </form>
      <section aria-labelledby="add-doc-h" className="flex flex-col gap-2">
        <h2 id="add-doc-h" className="m-0 text-18 font-bold">
          {A.docsTitle}
        </h2>
        <DocRow reference={detail.reference} kind="other" label={c.wizard.docs.kinds.other} doc={null} allowName />
      </section>
    </IndividualFrame>
  );
}
