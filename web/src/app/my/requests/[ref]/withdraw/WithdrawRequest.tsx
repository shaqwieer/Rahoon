"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import type { MyRequestDetail } from "@/lib/api/requests";

export function WithdrawRequest({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const X = c.withdraw;
  const router = useRouter();
  const ref = encodeURIComponent(detail.reference);
  const [reason, setReason] = useState("");
  const send = useRequestSubmit();

  const confirm = async () => {
    const res = await send.run((key) => apiSend("POST", `/my/requests/${ref}/withdraw`, { reason: reason.trim() || null }, { idempotencyKey: key }));
    if (res.ok) {
      router.push(detail.status === "draft" ? "/my" : `/my/requests/${ref}`);
      router.refresh();
    }
  };

  return (
    <IndividualFrame title={X.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}` }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{X.heading}</h1>
      <p className="m-0 text-17 leading-7">{X.body}</p>
      <Textarea label={X.reason} optionalMark rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      {send.error ? <Alert tone="err">{send.error}</Alert> : null}
      <Button variant="sensitive" size="xl" fullWidth loading={send.busy} onClick={() => void confirm()}>
        {X.confirm}
      </Button>
      <Button href={`/my/requests/${ref}`} variant="secondary" size="xl" fullWidth>
        {X.keep}
      </Button>
    </IndividualFrame>
  );
}
