"use client";

import { useState } from "react";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Select, Textarea, TextField } from "@/components/ui/Field";
import { Tag } from "@/components/ui/Status";
import type { TeamRequestDetail } from "@/lib/api/team";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { useTeamAction } from "./teamActions";

function Footer({ onClose, busy, label, onSubmit }: { onClose: () => void; busy: boolean; label: string; onSubmit: () => void }) {
  const c = useTeamCopy();
  return (
    <div className="flex justify-end gap-2">
      <Button variant="secondary" onClick={onClose}>
        {c.cancel}
      </Button>
      <Button loading={busy} onClick={onSubmit}>
        {label}
      </Button>
    </div>
  );
}

/** T08 answer (objection or complaint): outcome + plain-language response, no promised time or result. */
export function AnswerDialog({ concernId, onClose }: { concernId: string | null; onClose: () => void }) {
  const c = useTeamCopy();
  const K = c.concerns;
  const [outcome, setOutcome] = useState("");
  const [response, setResponse] = useState("");
  const act = useTeamAction();
  const submit = async () => {
    if (!concernId) return;
    if (await act.run("POST", `/team/concerns/${concernId}/answer`, { outcome, response })) {
      setOutcome("");
      setResponse("");
      onClose();
    }
  };
  return (
    <Dialog open={concernId !== null} onClose={onClose} title={K.answerTitle} footer={<Footer onClose={onClose} busy={act.busy} label={c.send} onSubmit={() => void submit()} />}>
      <div className="flex flex-col gap-3">
        <Select label={K.outcome} placeholder="—" value={outcome} onChange={(e) => setOutcome(e.target.value)} options={Object.entries(K.outcomes).map(([value, label]) => ({ value, label }))} error={act.fieldErrors.outcome} />
        <Textarea label={K.response} help={K.responseHelp} rows={4} maxLength={2000} value={response} onChange={(e) => setResponse(e.target.value)} error={act.fieldErrors.response} />
        {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Dialog>
  );
}

export function ReferralDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const R = c.referral;
  const [type, setType] = useState("financial_counselling");
  const [name, setName] = useState("");
  const [note, setNote] = useState("");
  const [text, setText] = useState("");
  const act = useTeamAction();
  const submit = async () => {
    if (await act.run("POST", `${base}/referrals`, { specialistType: type, specialistName: name, note: note || null, applicantText: text })) onClose();
  };
  return (
    <Dialog open={open} onClose={onClose} title={R.title} footer={<Footer onClose={onClose} busy={act.busy} label={c.save} onSubmit={() => void submit()} />}>
      <div className="flex flex-col gap-3">
        <Alert tone="info" compact>
          {R.banner}
        </Alert>
        <Select label={R.type} value={type} onChange={(e) => setType(e.target.value)} options={Object.entries(R.types).map(([value, label]) => ({ value, label }))} />
        <TextField label={R.name} maxLength={200} value={name} onChange={(e) => setName(e.target.value)} error={act.fieldErrors.specialistName} />
        <Textarea label={R.note} rows={2} value={note} onChange={(e) => setNote(e.target.value)} help={c.messages.noteHelp} />
        <Textarea label={R.applicantText} rows={3} value={text} onChange={(e) => setText(e.target.value)} error={act.fieldErrors.applicantText} />
        {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Dialog>
  );
}

export function ReopenDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const R = c.reopen;
  const [reason, setReason] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={R.title}
      footer={<Footer onClose={onClose} busy={act.busy} label={R.submit} onSubmit={async () => (await act.run("POST", `${base}/reopen`, { reason })) && onClose()} />}
    >
      <Textarea label={R.reason} help={R.help} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} error={act.fieldErrors.reason} />
      {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

/** Concerns raised on this request + specialist referrals (team view). */
export function ConcernPanels({ detail, onRefer }: { detail: TeamRequestDetail; onRefer: () => void }) {
  const c = useTeamCopy();
  const K = c.concerns;
  const { numerals } = useI18n();
  const [answering, setAnswering] = useState<string | null>(null);
  const mine = detail.assignedToMe;
  if (detail.concerns.length === 0 && detail.referrals.length === 0 && !detail.can.refer) return null;
  return (
    <section aria-labelledby="concerns-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="concerns-h" className="m-0 text-17 font-bold">
          {K.section}
        </h2>
        {detail.can.refer ? (
          <Button size="sm" variant="secondary" icon="diversity_3" onClick={onRefer}>
            {c.referral.button}
          </Button>
        ) : null}
      </div>
      {detail.concerns.length === 0 ? <p className="m-0 text-14 text-muted">{K.empty}</p> : null}
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {detail.concerns.map((x) => (
          <li key={x.id} className="flex flex-col gap-1.5 rounded-md border border-line p-3 text-14">
            <span className="flex flex-wrap items-center gap-2 text-13">
              <Tag tone={x.kind === "complaint" ? "warn" : "info"}>{K.kinds[x.kind] ?? x.kind}</Tag>
              <Tag tone="neutral">{K.subjects[x.subject] ?? x.subject}</Tag>
              <bdi dir="ltr" className="font-mono">
                {x.reference}
              </bdi>
              <bdi dir="ltr" className="text-muted">
                {formatDateTime(x.createdAt, { numerals })}
              </bdi>
            </span>
            <p className="m-0 whitespace-pre-line">{x.text}</p>
            {x.responseText ? (
              <p className="m-0 rounded-sm bg-info-bg p-2">
                <strong>{x.outcome ? `${K.outcomes[x.outcome] ?? x.outcome} · ` : ""}</strong>
                {x.responseText}
                {x.respondedByLabel ? <span className="block text-12 text-muted">{K.answeredBy(x.respondedByLabel)}</span> : null}
              </p>
            ) : detail.can.answerConcerns ? (
              x.kind === "complaint" && mine ? (
                <span className="text-13 text-muted">{K.notYou}</span>
              ) : (
                <Button size="sm" className="self-start" onClick={() => setAnswering(x.id)}>
                  {K.answer}
                </Button>
              )
            ) : null}
          </li>
        ))}
      </ul>
      {detail.referrals.length > 0 ? (
        <>
          <h3 className="m-0 text-15 font-semibold">{c.referral.section}</h3>
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {detail.referrals.map((r, i) => (
              <li key={i} className="flex flex-col gap-1 rounded-md border border-line p-3 text-14">
                <strong>
                  {c.referral.types[r.specialistType] ?? r.specialistType} · {r.specialistName}
                </strong>
                <span>{r.applicantText}</span>
                {r.note ? <span className="text-13 text-muted">{r.note}</span> : null}
                <span className="text-12 text-muted">
                  {r.recordedByLabel} · <bdi dir="ltr">{formatDateTime(r.at, { numerals })}</bdi>
                </span>
              </li>
            ))}
          </ul>
        </>
      ) : null}
      <AnswerDialog concernId={answering} onClose={() => setAnswering(null)} />
    </section>
  );
}
