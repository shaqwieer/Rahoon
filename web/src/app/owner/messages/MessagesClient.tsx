"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent } from "react";
import type { OwnerAppointment, OwnerMessage } from "@/components/owner/types";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, DateText, Dialog, EmptyState, Icon, Textarea } from "@/components/ui";
import { apiSend } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { applyNumerals, type Numerals } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

function apptParts(iso: string, locale: "ar" | "en", numerals: Numerals) {
  const loc = locale === "en" ? "en-US" : "ar-SA-u-ca-gregory-nu-latn";
  const d = new Date(iso);
  const f = (o: Intl.DateTimeFormatOptions) => applyNumerals(new Intl.DateTimeFormat(loc, { timeZone: "Asia/Riyadh", ...o }).format(d).replace(/[؜‎‏]/g, ""), numerals);
  return { day: f({ day: "numeric" }), month: f({ month: "long" }), time: f({ hour: "numeric", minute: "2-digit", hour12: true }) };
}

/** D12 — appointment strip(s), the owner-channel conversation (owner at the end, tinted) and the composer. */
export function MessagesClient({ appointments, messages }: { appointments: OwnerAppointment[]; messages: OwnerMessage[] }) {
  const c = useOwnerCopy();
  const M = c.messages;
  const router = useRouter();
  const [announce, setAnnounce] = useState<string | null>(null);
  const endRef = useRef<HTMLLIElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" });
  }, [messages.length]);

  return (
    <div className="flex flex-1 flex-col gap-3">
      {appointments.length ? (
        <section aria-label={M.appointments} className="flex flex-col gap-2">
          {appointments.map((a) => (
            <AppointmentStrip key={a.id} a={a} onDone={(m) => setAnnounce(m)} />
          ))}
        </section>
      ) : null}
      <div role="status" aria-live="polite" className="sr-only">
        {announce}
      </div>

      {messages.length === 0 ? (
        <EmptyState icon="chat" title={M.emptyTitle} body={M.emptyBody} />
      ) : (
        <ol role="log" aria-label={M.log} className="m-0 flex list-none flex-col gap-3 p-0">
          {messages.map((m, i) => (
            <li key={m.id} ref={i === messages.length - 1 ? endRef : undefined} className={cn("flex max-w-[84%] flex-col gap-1", m.mine ? "self-end" : "self-start")}>
              <ServerText
                as="div"
                text={m.body}
                className={cn("rounded-[14px] border px-3.5 py-3 text-16 leading-[26px] whitespace-pre-wrap", m.mine ? "border-rust-200 bg-rust-50" : "border-line bg-white")}
              />
              <span className="text-12 text-muted">
                {m.mine ? M.you : <ServerText text={m.author} />} · <DateText value={m.at} mode="datetime" />
                {m.mine && m.read ? ` · ${M.read}` : null}
              </span>
            </li>
          ))}
        </ol>
      )}

      <Composer
        onSent={() => {
          setAnnounce(M.sent);
          router.refresh();
        }}
      />
    </div>
  );
}

function AppointmentStrip({ a, onDone }: { a: OwnerAppointment; onDone: (message: string) => void }) {
  const c = useOwnerCopy();
  const M = c.messages;
  const { locale, numerals } = useI18n();
  const router = useRouter();
  const p = apptParts(a.startsAt, locale, numerals);
  const confirm = useSubmit();
  const resched = useSubmit();
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState("");
  const canConfirm = a.status === "Proposed" || a.status === "Rescheduled";

  const doConfirm = async () => {
    const res = await confirm.run((k) => apiSend<{ status: string }>("POST", `/owner/appointments/${a.id}/confirm`, undefined, { idempotencyKey: k }));
    if (res.ok) {
      onDone(M.confirmed);
      router.refresh();
    }
  };
  const doReschedule = async () => {
    const res = await resched.run((k) => apiSend<{ status: string }>("POST", `/owner/appointments/${a.id}/reschedule`, { note: note.trim() || null }, { idempotencyKey: k }));
    if (res.ok) {
      setOpen(false);
      setNote("");
      onDone(M.rescheduled);
      router.refresh();
    }
  };

  return (
    <div className="flex flex-col gap-2 rounded-[12px] border border-line bg-white px-3.5 py-3">
      <div className="flex items-center gap-3">
        <span aria-hidden="true" className="flex size-12 flex-none flex-col items-center justify-center rounded-[8px] bg-rust-50 leading-[1.1] font-bold text-rust-700">
          <span className="text-18">{p.day}</span>
          <span className="text-11">{p.month}</span>
        </span>
        <div className="flex min-w-0 flex-1 flex-col">
          <strong className="text-15">
            <span className="sr-only">
              {p.day} {p.month} ·{" "}
            </span>
            {a.type === "call" ? M.call : M.visit} · <bdi>{p.time}</bdi>
          </strong>
          <ServerText text={a.statusText} className="text-13 text-muted" />
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        {canConfirm ? (
          <Button size="lg" variant="secondary" loading={confirm.busy} onClick={() => void doConfirm()}>
            {M.confirm}
          </Button>
        ) : null}
        <Button size="lg" variant="text" className="px-1" onClick={() => setOpen(true)}>
          {M.reschedule}
        </Button>
      </div>
      {confirm.error ? (
        <Alert tone="err" role="alert" compact>
          {confirm.error}
        </Alert>
      ) : null}
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title={M.rescheduleTitle}
        footer={
          <>
            <Button size="lg" loading={resched.busy} loadingLabel={c.sending} onClick={() => void doReschedule()}>
              {M.rescheduleSend}
            </Button>
            <Button size="lg" variant="secondary" onClick={() => setOpen(false)}>
              {c.back}
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4 p-5">
          <p className="m-0 text-16 leading-[26px]">{M.rescheduleBody}</p>
          <Textarea
            label={M.rescheduleNote}
            optionalMark
            maxLength={500}
            value={note}
            onChange={(e) => {
              setNote(e.target.value);
              resched.resetKey();
            }}
            className="text-16"
          />
          <div aria-live="assertive">{resched.error ? <Alert tone="err" role="none">{resched.error}</Alert> : null}</div>
        </div>
      </Dialog>
    </div>
  );
}

function Composer({ onSent }: { onSent: () => void }) {
  const c = useOwnerCopy();
  const M = c.messages;
  const [body, setBody] = useState("");
  const { run, busy, error, resetKey } = useSubmit();
  const ref = useRef<HTMLTextAreaElement>(null);

  const send = async (e: FormEvent) => {
    e.preventDefault();
    const text = body.trim();
    if (!text) {
      ref.current?.focus();
      return;
    }
    const res = await run((k) => apiSend<{ sent: boolean }>("POST", "/owner/messages", { body: text }, { idempotencyKey: k }));
    if (res.ok) {
      setBody("");
      onSent();
    }
  };

  return (
    <form noValidate onSubmit={send} className="sticky bottom-0 -mx-[18px] mt-auto flex flex-col gap-1.5 border-t border-divider bg-white px-3 pt-2.5 pb-4 md:mx-0 md:rounded-[12px] md:border">
      <div aria-live="assertive">{error ? <Alert tone="err" role="none" compact>{error}</Alert> : null}</div>
      <div className="flex items-end gap-2">
        <label htmlFor="owner-composer" className="sr-only">
          {M.label}
        </label>
        <textarea
          id="owner-composer"
          ref={ref}
          rows={1}
          maxLength={2000}
          value={body}
          placeholder={M.placeholder}
          aria-describedby="composer-hint"
          onChange={(e) => {
            setBody(e.target.value);
            resetKey();
          }}
          className="max-h-40 min-h-[50px] flex-1 resize-y rounded-[8px] border border-line-strong bg-white px-3 py-3 text-16 leading-6 placeholder:text-muted focus:border-2 focus:border-ink focus:px-[11px] focus:py-[11px]"
        />
        <button
          type="submit"
          aria-label={M.send}
          aria-busy={busy || undefined}
          disabled={busy}
          className="flex size-[50px] flex-none items-center justify-center rounded-[8px] bg-rust text-white hover:bg-rust-700 disabled:bg-track disabled:text-soft"
        >
          <Icon name={busy ? "progress_activity" : "send"} size={22} className={busy ? "animate-rh-spin" : undefined} />
        </button>
      </div>
      <span id="composer-hint" className="flex items-center gap-1 text-13 text-muted">
        <Icon name="schedule" size={16} />
        {M.replyHint}
      </span>
    </form>
  );
}
