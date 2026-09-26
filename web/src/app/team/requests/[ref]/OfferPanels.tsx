"use client";

import { useState, type ReactNode } from "react";
import { StepUpDialog } from "@/components/case/StepUpDialog";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog, Drawer } from "@/components/ui/Dialog";
import { AmountField, Checkbox, Select, Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { Tag } from "@/components/ui/Status";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { TeamOffer, TeamRequestDetail } from "@/lib/api/team";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { useRouter } from "next/navigation";
import { useTeamAction } from "./teamActions";

const CHECKLIST = ["amounts_match_letter", "terms_match_letter", "reference_and_date_match", "effect_text_accurate"];

function OfferValues({ offer }: { offer: TeamOffer }) {
  const c = useTeamCopy();
  const O = c.offers;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const rows: Array<{ key: string; value: ReactNode }> = [{ key: O.path, value: O.paths[offer.path] }];
  if (offer.newInstallment !== null) rows.push({ key: O.newInstallment, value: formatMoney(offer.newInstallment, fmt) });
  if (offer.termMonths !== null) rows.push({ key: O.termMonths, value: offer.termMonths });
  if (offer.startText) rows.push({ key: O.startText, value: offer.startText });
  if (offer.settlementAmount !== null) rows.push({ key: O.settlementAmount, value: formatMoney(offer.settlementAmount, fmt) });
  if (offer.paymentConditions) rows.push({ key: O.paymentConditions, value: offer.paymentConditions });
  if (offer.remainingText) rows.push({ key: O.remainingText, value: offer.remainingText });
  if (offer.saleTerms) rows.push({ key: O.saleTerms, value: offer.saleTerms });
  if (offer.conditions) rows.push({ key: O.conditions, value: offer.conditions });
  rows.push({ key: O.effectText, value: <span className="whitespace-pre-line">{offer.effectText}</span> });
  rows.push({ key: O.lenderReference, value: <bdi dir="ltr" className="font-mono">{offer.lenderReference}</bdi> });
  rows.push({ key: O.lenderLetterDate, value: <bdi dir="ltr">{formatDate(offer.lenderLetterDate, fmt)}</bdi> });
  if (offer.lenderValidityText) rows.push({ key: O.validity, value: offer.lenderValidityText });
  return <KeyValueList rows={rows} />;
}

/** T05 list + record, T06 verification (another member, step-up), T07 response relay. */
export function OfferPanels({ detail, base }: { detail: TeamRequestDetail; base: string }) {
  const c = useTeamCopy();
  const O = c.offers;
  const { numerals } = useI18n();
  const [recordOpen, setRecordOpen] = useState(false);
  const [relayOpen, setRelayOpen] = useState(false);
  const letters = new Map(detail.documents.map((d) => [d.id, d]));
  const pending = detail.offers.find((o) => o.status === "pendingverification");

  return (
    <>
      {detail.offers.length > 0 || detail.can.recordOffer ? (
        <section aria-labelledby="offers-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="offers-h" className="m-0 text-17 font-bold">
              {O.title}
            </h2>
            {detail.can.recordOffer ? (
              <Button size="sm" icon="add" onClick={() => setRecordOpen(true)}>
                {O.record}
              </Button>
            ) : null}
          </div>
          {detail.offers.length === 0 ? <p className="m-0 text-14 text-muted">{O.empty}</p> : null}
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {detail.offers.map((o) => {
              const letter = letters.get(o.letterDocumentId);
              return (
                <li key={o.id} className="flex flex-col gap-2 rounded-md border border-line p-3">
                  <div className="flex flex-wrap items-center gap-2 text-13">
                    <strong className="text-14">{O.version(o.versionNo)}</strong>
                    <Tag tone={o.status === "published" ? "ok" : o.status === "returned" ? "err" : o.status === "pendingverification" ? "warn" : "neutral"}>
                      {O.status[o.status]}
                    </Tag>
                    <span className="text-muted">
                      {O.recordedBy(o.recordedByLabel)} · <bdi dir="ltr">{formatDateTime(o.recordedAt, { numerals })}</bdi>
                    </span>
                    {o.verifiedByLabel ? <span className="text-muted">· {O.verifiedBy(o.verifiedByLabel)}</span> : null}
                  </div>
                  {letter?.versionId ? (
                    <a href={`/api${base}/documents/${letter.versionId}/file`} className="inline-flex items-center gap-1 self-start text-14 font-semibold">
                      <Icon name="mail" size={18} />
                      {c.verify.openLetter}
                    </a>
                  ) : null}
                  <OfferValues offer={o} />
                  {o.returnReason ? <Alert tone="err" title={O.returnReason}>{o.returnReason}</Alert> : null}
                </li>
              );
            })}
          </ul>
          {pending && detail.can.verify ? <VerifyPanel offer={pending} base={base} letterVersionId={letters.get(pending.letterDocumentId)?.versionId ?? null} /> : null}
          {pending && pending.recordedByMe ? <p className="m-0 text-13 text-muted">{O.sameMemberNote}</p> : null}
        </section>
      ) : null}

      {detail.responses.length > 0 ? (
        <section aria-labelledby="resp-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="resp-h" className="m-0 text-17 font-bold">
              {c.responses.title}
            </h2>
            {detail.can.relay ? (
              <Button size="sm" icon="forward_to_inbox" onClick={() => setRelayOpen(true)}>
                {c.responses.relay}
              </Button>
            ) : null}
          </div>
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
            {detail.responses.map((x) => (
              <li key={x.id} className="flex flex-col gap-1 rounded-md border border-line p-3 text-14">
                <span className="flex flex-wrap items-center gap-2">
                  <strong>{c.responses.kinds[x.kind] ?? x.kind}</strong>
                  <bdi dir="ltr" className="font-mono text-13 text-muted">
                    {x.reference}
                  </bdi>
                  {x.otpVerifiedAt ? (
                    <Tag tone="ok" icon="sms">
                      {c.responses.otp}
                    </Tag>
                  ) : null}
                  <Tag tone={x.relayedAt ? "ok" : "warn"}>{x.relayedAt ? c.responses.relayed : c.responses.notRelayed}</Tag>
                </span>
                {x.text ? <p className="m-0 whitespace-pre-line">{x.text}</p> : null}
                {x.consentTextSnapshot ? <p className="m-0 rounded-sm bg-subtle p-2 text-13">{x.consentTextSnapshot}</p> : null}
                <bdi dir="ltr" className="text-12 text-muted">
                  {formatDateTime(x.at, { numerals })}
                </bdi>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <RecordOfferDrawer open={recordOpen} onClose={() => setRecordOpen(false)} base={base} detail={detail} />
      <RelayDrawer open={relayOpen} onClose={() => setRelayOpen(false)} base={base} />
    </>
  );
}

function VerifyPanel({ offer, base, letterVersionId }: { offer: TeamOffer; base: string; letterVersionId: string | null }) {
  const c = useTeamCopy();
  const V = c.verify;
  const router = useRouter();
  const key = useIdempotencyKey();
  const [checked, setChecked] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stepUp, setStepUp] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);

  const publish = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `${base}/offers/${offer.id}/verify`, { decision: "publish", checklist: checked }, { idempotencyKey: key.get() });
      key.reset();
      // The verifier's access ends once nothing awaits their check: back to the verification queue.
      router.push("/team/verify");
    } catch (e) {
      key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else setError(isApiError(e) ? [e.title, ...(e.reasons ?? []), ...Object.values(e.errors ?? {}).flat()].filter(Boolean).join(" — ") : c.genericError);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-3 rounded-md border-2 border-warn-line bg-warn-bg p-4">
      <h3 className="m-0 text-16 font-bold">{V.panel}</h3>
      <span className="text-13">{V.source}</span>
      {letterVersionId ? (
        <a href={`/api${base}/documents/${letterVersionId}/file`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 self-start text-14 font-semibold">
          <Icon name="open_in_new" size={18} />
          {V.openLetter}
        </a>
      ) : null}
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">{V.checklist}</legend>
        {CHECKLIST.map((i) => (
          <Checkbox key={i} label={V.items[i]} checked={checked.includes(i)} onChange={(e) => setChecked((x) => (e.target.checked ? [...x, i] : x.filter((y) => y !== i)))} />
        ))}
      </fieldset>
      {error ? <Alert tone="err">{error}</Alert> : null}
      <div className="flex flex-wrap gap-2">
        <Button loading={busy} softDisabled={checked.length !== CHECKLIST.length} onClick={() => void publish()} icon="verified">
          {V.publish}
        </Button>
        <Button variant="secondary" onClick={() => setReturnOpen(true)}>
          {V.return}
        </Button>
      </div>
      <StepUpDialog
        open={stepUp}
        onClose={() => setStepUp(false)}
        onVerified={() => {
          setStepUp(false);
          void publish();
        }}
      />
      <ReturnDialog open={returnOpen} onClose={() => setReturnOpen(false)} base={base} offerId={offer.id} />
    </div>
  );
}

function ReturnDialog({ open, onClose, base, offerId }: { open: boolean; onClose: () => void; base: string; offerId: string }) {
  const c = useTeamCopy();
  const [reason, setReason] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={c.verify.returnTitle}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={async () => (await act.run("POST", `${base}/offers/${offerId}/verify`, { decision: "return", reason }, undefined, "/team/verify")) && onClose()}>
            {c.send}
          </Button>
        </div>
      }
    >
      <Textarea label={c.verify.reason} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} error={act.fieldErrors.reason} />
      {act.error && !act.fieldErrors.reason ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

function RecordOfferDrawer({ open, onClose, base, detail }: { open: boolean; onClose: () => void; base: string; detail: TeamRequestDetail }) {
  const c = useTeamCopy();
  const O = c.offers;
  const letters = detail.documents.filter((d) => d.kind === "lender_letter");
  const [path, setPath] = useState("p1");
  const [letterId, setLetterId] = useState("");
  const [newInstallment, setNewInstallment] = useState<number | null>(null);
  const [termMonths, setTermMonths] = useState("");
  const [startText, setStartText] = useState("");
  const [settlementAmount, setSettlementAmount] = useState<number | null>(null);
  const [paymentConditions, setPaymentConditions] = useState("");
  const [remainingText, setRemainingText] = useState("");
  const [saleTerms, setSaleTerms] = useState("");
  const [conditions, setConditions] = useState("");
  const [effectText, setEffectText] = useState("");
  const [lenderReference, setLenderReference] = useState("");
  const [lenderLetterDate, setLenderLetterDate] = useState("");
  const [validity, setValidity] = useState("");
  const [shareLetter, setShareLetter] = useState(true);
  const act = useTeamAction();
  const fe = act.fieldErrors;

  const submit = async () => {
    const ok = await act.run("POST", `${base}/offers`, {
      path,
      newInstallment,
      termMonths: termMonths ? Number(termMonths) : null,
      startText: startText || null,
      settlementAmount,
      paymentConditions: paymentConditions || null,
      remainingText: remainingText || null,
      saleTerms: saleTerms || null,
      conditions: conditions || null,
      effectText,
      lenderReference,
      lenderLetterDate: lenderLetterDate || null,
      lenderValidityText: validity || null,
      letterDocumentId: letterId || null,
      shareLetter,
    });
    if (ok) onClose();
  };

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={O.recordTitle}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={() => void submit()}>
            {O.submit}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        <Select
          label={O.letter}
          placeholder={O.letterPick}
          value={letterId}
          onChange={(e) => setLetterId(e.target.value)}
          options={letters.map((d) => ({ value: d.id, label: `${d.name}${d.fileName ? ` · ${d.fileName}` : ""}` }))}
          help={O.letterHelp}
          error={fe.letterDocumentId}
        />
        <Select label={O.path} value={path} onChange={(e) => setPath(e.target.value)} options={Object.entries(O.paths).map(([value, label]) => ({ value, label }))} error={fe.path} />
        {path === "p1" ? (
          <>
            <AmountField label={O.newInstallment} value={newInstallment} onValueChange={setNewInstallment} error={fe.newInstallment} />
            <TextField label={O.termMonths} inputMode="numeric" ltr value={termMonths} onChange={(e) => setTermMonths(e.target.value.replace(/\D/g, ""))} error={fe.termMonths} />
            <TextField label={O.startText} maxLength={300} value={startText} onChange={(e) => setStartText(e.target.value)} />
          </>
        ) : null}
        {path === "p2" ? (
          <>
            <AmountField label={O.settlementAmount} value={settlementAmount} onValueChange={setSettlementAmount} error={fe.settlementAmount} />
            <Textarea label={O.paymentConditions} rows={2} value={paymentConditions} onChange={(e) => setPaymentConditions(e.target.value)} />
            <Textarea label={O.remainingText} rows={2} value={remainingText} onChange={(e) => setRemainingText(e.target.value)} />
          </>
        ) : null}
        {path === "p3" ? <Textarea label={O.saleTerms} rows={3} value={saleTerms} onChange={(e) => setSaleTerms(e.target.value)} error={fe.saleTerms} /> : null}
        <Textarea label={O.conditions} rows={2} value={conditions} onChange={(e) => setConditions(e.target.value)} />
        <Textarea label={O.effectText} rows={3} maxLength={2000} value={effectText} onChange={(e) => setEffectText(e.target.value)} error={fe.effectText} />
        <div className="grid gap-3 sm:grid-cols-2">
          <TextField label={O.lenderReference} ltr mono maxLength={100} value={lenderReference} onChange={(e) => setLenderReference(e.target.value)} error={fe.lenderReference} />
          <TextField label={O.lenderLetterDate} type="date" ltr value={lenderLetterDate} onChange={(e) => setLenderLetterDate(e.target.value)} error={fe.lenderLetterDate} />
        </div>
        <TextField label={O.validity} help={O.validityHelp} maxLength={300} value={validity} onChange={(e) => setValidity(e.target.value)} />
        <Checkbox label={O.shareLetter} checked={shareLetter} onChange={(e) => setShareLetter(e.target.checked)} />
        <p className="m-0 text-13 text-muted">{O.sameMemberNote}</p>
        {act.error && Object.keys(fe).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

const toLocalInput = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

function RelayDrawer({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const C = c.coordination;
  const [channel, setChannel] = useState("email");
  const [occurredAt, setOccurredAt] = useState(() => toLocalInput(new Date()));
  const [counterpart, setCounterpart] = useState("");
  const [summary, setSummary] = useState("");
  const [applicantText, setApplicantText] = useState("");
  const act = useTeamAction();
  const submit = async () => {
    const ok = await act.run("POST", `${base}/relay`, {
      channel,
      occurredAt: occurredAt ? new Date(occurredAt).toISOString() : null,
      counterpart,
      summary,
      applicantText: applicantText || null,
    });
    if (ok) onClose();
  };
  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={c.responses.relayTitle}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={() => void submit()}>
            {c.save}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        <Alert tone="info" compact>
          {C.banner}
        </Alert>
        <Select label={C.channel} value={channel} onChange={(e) => setChannel(e.target.value)} options={Object.entries(C.channels).map(([value, label]) => ({ value, label }))} />
        <TextField label={C.occurredAt} type="datetime-local" ltr value={occurredAt} onChange={(e) => setOccurredAt(e.target.value)} error={act.fieldErrors.occurredAt} />
        <TextField label={C.counterpart} help={C.counterpartHelp} value={counterpart} onChange={(e) => setCounterpart(e.target.value)} error={act.fieldErrors.counterpart} />
        <Textarea label={C.summary} rows={3} value={summary} onChange={(e) => setSummary(e.target.value)} error={act.fieldErrors.summary} />
        <Textarea label={c.responses.applicantText} help={c.responses.applicantTextHelp} rows={2} value={applicantText} onChange={(e) => setApplicantText(e.target.value)} />
        {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

export function CloseDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const X = c.close;
  const [outcomeCode, setOutcomeCode] = useState("");
  const [summary, setSummary] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={X.title}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={async () => (await act.run("POST", `${base}/close`, { outcomeCode, summary })) && onClose()}>
            {X.submit}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        <Select label={X.outcome} placeholder="—" value={outcomeCode} onChange={(e) => setOutcomeCode(e.target.value)} options={Object.entries(X.outcomes).map(([value, label]) => ({ value, label }))} error={act.fieldErrors.outcomeCode} />
        <Textarea label={X.summary} help={X.summaryHelp} rows={4} maxLength={1500} value={summary} onChange={(e) => setSummary(e.target.value)} error={act.fieldErrors.summary} />
        {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Dialog>
  );
}
