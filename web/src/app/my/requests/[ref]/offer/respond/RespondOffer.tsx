"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { ChoiceCard } from "@/components/owner/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import type { MyRequestDetail } from "@/lib/api/requests";

export function RespondOffer({ detail, decline }: { detail: MyRequestDetail; decline: boolean }) {
  const c = useRequestCopy();
  const R = c.respond;
  const router = useRouter();
  const ref = encodeURIComponent(detail.reference);
  const [kind, setKind] = useState<"question" | "counter">("question");
  const [text, setText] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const send = useRequestSubmit();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!decline && !text.trim()) {
      setFieldError(R.textError);
      return;
    }
    const res = await send.run((key) =>
      apiSend("POST", `/my/requests/${ref}/offer/respond`, { kind: decline ? "decline" : kind, text: text.trim() || null }, { idempotencyKey: key }),
    );
    if (res.ok) {
      router.push(`/my/requests/${ref}`);
      router.refresh();
    }
  };

  return (
    <IndividualFrame title={decline ? R.declineTitle : R.questionTitle} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}/offer` }}>
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <h1 className="m-0 text-24 leading-9 font-bold">{decline ? R.declineHeading : R.questionHeading}</h1>
        {decline ? <p className="m-0 text-17 leading-7">{R.declineBody}</p> : null}
        {!decline ? (
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="sr-only">{R.questionHeading}</legend>
            <ChoiceCard type="radio" name="kind" checked={kind === "question"} onChange={() => setKind("question")} label={R.kindQuestion} />
            <ChoiceCard type="radio" name="kind" checked={kind === "counter"} onChange={() => setKind("counter")} label={R.kindCounter} />
          </fieldset>
        ) : null}
        <Textarea
          label={decline ? R.declineReason : R.text}
          optionalMark={decline}
          rows={4}
          maxLength={2000}
          value={text}
          onChange={(e) => {
            setText(e.target.value);
            setFieldError(null);
          }}
          error={fieldError ?? undefined}
        />
        {send.error ? <Alert tone="err">{send.error}</Alert> : null}
        <Button type="submit" size="xl" fullWidth loading={send.busy} variant={decline ? "secondary" : "primary"}>
          {decline ? R.confirmDecline : R.send}
        </Button>
        <Button href={`/my/requests/${ref}/offer`} variant="text" size="lg" className="self-center text-15">
          {R.back}
        </Button>
      </form>
    </IndividualFrame>
  );
}
