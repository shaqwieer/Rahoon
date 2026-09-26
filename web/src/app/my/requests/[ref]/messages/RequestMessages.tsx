"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

export interface MyMessages {
  reference: string;
  canSend: boolean;
  items: Array<{ id: string; authorKind: "applicant" | "team"; author: string; body: string; at: string }>;
}

export function RequestMessages({ data }: { data: MyMessages }) {
  const c = useRequestCopy();
  const M = c.messages;
  const { numerals } = useI18n();
  const router = useRouter();
  const [text, setText] = useState("");
  const send = useRequestSubmit();
  const ref = encodeURIComponent(data.reference);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return;
    const res = await send.run((key) => apiSend("POST", `/my/requests/${ref}/messages`, { text: text.trim() }, { idempotencyKey: key }));
    if (res.ok) {
      setText("");
      router.refresh();
    }
  };

  return (
    <IndividualFrame title={M.title} sub={<bdi dir="ltr">{data.reference}</bdi>} back={{ href: `/my/requests/${ref}` }}>
      <h1 className="sr-only">{M.title}</h1>
      {data.items.length === 0 ? <p className="m-0 text-16 text-muted">{M.empty}</p> : null}
      <ol className="m-0 flex list-none flex-col gap-2.5 p-0">
        {data.items.map((m) => (
          <li key={m.id} className={cn("flex max-w-[85%] flex-col gap-1 rounded-[12px] px-3.5 py-2.5", m.authorKind === "team" ? "self-start bg-white border border-line" : "self-end bg-rust-50")}>
            <span className="text-12 font-semibold text-muted">{m.authorKind === "team" ? M.team : M.you}</span>
            <p className="m-0 text-16 leading-7 whitespace-pre-line">{m.body}</p>
            <bdi dir="ltr" className="text-12 text-muted">
              {formatDateTime(m.at, { numerals })}
            </bdi>
          </li>
        ))}
      </ol>
      {data.canSend ? (
        <form noValidate onSubmit={submit} className="flex flex-col gap-2.5">
          <Textarea label={M.write} rows={3} maxLength={2000} value={text} onChange={(e) => setText(e.target.value)} />
          {send.error ? <Alert tone="err">{send.error}</Alert> : null}
          <Button type="submit" size="xl" fullWidth loading={send.busy} disabled={!text.trim()}>
            {M.send}
          </Button>
        </form>
      ) : null}
    </IndividualFrame>
  );
}
