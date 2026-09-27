"use client";

import { useRouter } from "next/navigation";
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
import type { ExecutionRecord, PaymentReport, TeamRequestDetail } from "@/lib/api/team";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { useTeamAction } from "./teamActions";

const CHECKLIST = ["values_match_source", "reference_and_date_match", "kind_is_correct", "explanation_accurate"];
const KINDS: ExecutionRecord["kind"][] = ["agreement", "payment_confirmation", "lender_notice", "closure_document"];
type Draft = { kind: ExecutionRecord["kind"]; answersReportId?: string; scheduleItemNo?: number | null; amount?: number };

const statusTone = (s: ExecutionRecord["status"]) => (s === "published" ? "ok" : s === "returned" ? "err" : s === "pending_verification" ? "warn" : "neutral");

/**
 * Phase 1A-2 step 2 (design request D-7, «بانتظار اعتماد التصميم»): the «التنفيذ» section of T02. The team records what the
 * lender sends (T09/T10), another member verifies each record (T06 pattern, step-up), and the individual's payment reports
 * are answered with a note or linked to the lender's confirmation (T11). Nothing here says Rahoon received money.
 */
export function ExecutionPanels({ detail, base }: { detail: TeamRequestDetail; base: string }) {
  const c = useTeamCopy();
  const E = c.execution;
  const [draft, setDraft] = useState<Draft | null>(null);
  const [noteFor, setNoteFor] = useState<PaymentReport | null>(null);
  const x = detail.execution;
  const docs = new Map(detail.documents.map((d) => [d.id, d]));
  if (x.records.length === 0 && x.paymentReports.length === 0 && !detail.can.recordExecution) return null;
  const relevant = x.relevantClosureKinds.map((k) => E.documentKinds[k] ?? k).join("، ");

  return (
    <section aria-labelledby="exec-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="exec-h" className="m-0 text-17 font-bold">
          {E.title}
        </h2>
        <Tag tone="neutral" icon="design_services">
          {E.designPending}
        </Tag>
      </div>
      <Alert tone="info" compact>
        {E.banner}
      </Alert>
      {relevant ? <p className="m-0 text-13 text-muted">{E.relevant(relevant)}</p> : null}
      {detail.can.recordExecution ? (
        <div className="flex flex-wrap gap-2">
          {KINDS.map((k) => (
            <Button key={k} size="sm" variant={k === "agreement" ? "primary" : "secondary"} icon="add" onClick={() => setDraft({ kind: k })}>
              {E.record[k]}
            </Button>
          ))}
        </div>
      ) : null}

      {x.records.length === 0 ? <p className="m-0 text-14 text-muted">{E.empty}</p> : null}
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {x.records.map((r) => (
          <RecordItem key={r.id} record={r} base={base} sourceVersionId={docs.get(r.sourceDocumentId)?.versionId ?? null} canVerify={detail.can.verifyExecution} />
        ))}
      </ul>

      {x.paymentReports.length > 0 ? (
        <div className="flex flex-col gap-2 border-t border-divider pt-3">
          <h3 className="m-0 text-15 font-bold">{E.reports}</h3>
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
            {x.paymentReports.map((p) => (
              <ReportItem
                key={p.id}
                report={p}
                base={base}
                proofVersionId={docs.get(p.proofDocumentId)?.versionId ?? null}
                canAct={detail.can.recordExecution}
                onNote={() => setNoteFor(p)}
                onConfirm={() => setDraft({ kind: "payment_confirmation", answersReportId: p.id, scheduleItemNo: p.scheduleItemNo, amount: p.amount })}
              />
            ))}
          </ul>
        </div>
      ) : null}

      {draft ? <RecordDrawer key={`${draft.kind}-${draft.answersReportId ?? ""}`} draft={draft} onClose={() => setDraft(null)} base={base} detail={detail} /> : null}
      <NoteDialog report={noteFor} onClose={() => setNoteFor(null)} base={base} />
    </section>
  );
}

function RecordValues({ record }: { record: ExecutionRecord }) {
  const c = useTeamCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const rows: Array<{ key: string; value: ReactNode }> = [];
  if (record.path) rows.push({ key: c.offers.path, value: c.offers.paths[record.path] ?? record.path });
  if (record.activationDate) rows.push({ key: E.activationDate, value: <bdi dir="ltr">{formatDate(record.activationDate, fmt)}</bdi> });
  if (record.newInstallment !== null) rows.push({ key: E.newInstallment, value: formatMoney(record.newInstallment, fmt) });
  if (record.termMonths !== null) rows.push({ key: E.termMonths, value: record.termMonths });
  if (record.settlementAmount !== null) rows.push({ key: E.settlementAmount, value: formatMoney(record.settlementAmount, fmt) });
  if (record.termsText) rows.push({ key: E.termsText, value: record.termsText });
  if (record.amount !== null) rows.push({ key: E.amount, value: formatMoney(record.amount, fmt) });
  if (record.receivedOn) rows.push({ key: E.receivedOn, value: <bdi dir="ltr">{formatDate(record.receivedOn, fmt)}</bdi> });
  if (record.scheduleItemNo !== null) rows.push({ key: E.scheduleItemNo, value: record.scheduleItemNo });
  if (record.noticeCategory) rows.push({ key: E.noticeCategory, value: E.noticeCategories[record.noticeCategory] ?? record.noticeCategory });
  if (record.documentKind) rows.push({ key: E.documentKind, value: E.documentKinds[record.documentKind] ?? record.documentKind });
  rows.push({ key: E.summaryText, value: <span className="whitespace-pre-line">{record.summaryText}</span> });
  rows.push({ key: E.explanationText, value: <span className="whitespace-pre-line">{record.explanationText}</span> });
  rows.push({ key: E.lenderReference, value: <bdi dir="ltr" className="font-mono">{record.lenderReference}</bdi> });
  rows.push({ key: E.lenderDate, value: <bdi dir="ltr">{formatDate(record.lenderDate, fmt)}</bdi> });
  if (record.correctionReason) rows.push({ key: E.correctionReason, value: record.correctionReason });
  return <KeyValueList rows={rows} />;
}

function RecordItem({ record, base, sourceVersionId, canVerify }: { record: ExecutionRecord; base: string; sourceVersionId: string | null; canVerify: boolean }) {
  const c = useTeamCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  return (
    <li className={`flex flex-col gap-2 rounded-md border border-line p-3 ${record.status === "superseded" ? "opacity-70" : ""}`}>
      <div className="flex flex-wrap items-center gap-2 text-13">
        <strong className="text-14">{E.kinds[record.kind]}</strong>
        <Tag tone={statusTone(record.status)}>{E.status[record.status]}</Tag>
        <span className="text-muted">
          {E.recordedBy(record.recordedByLabel)} · <bdi dir="ltr">{formatDateTime(record.recordedAt, fmt)}</bdi>
        </span>
        {record.verifiedByLabel ? <span className="text-muted">· {E.verifiedBy(record.verifiedByLabel)}</span> : null}
      </div>
      {sourceVersionId ? (
        <a href={`/api${base}/documents/${sourceVersionId}/file`} className="inline-flex items-center gap-1 self-start text-14 font-semibold">
          <Icon name="mail" size={18} />
          {E.openSource}
        </a>
      ) : null}
      <RecordValues record={record} />
      {record.schedule.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-13">
            <caption className="pb-1 text-start font-semibold">{E.scheduleCaption}</caption>
            <tbody>
              {record.schedule.map((s) => (
                <tr key={s.no} className="border-t border-divider">
                  <td className="py-1 pe-3">{E.installmentNo(s.no)}</td>
                  <td className="py-1 pe-3">
                    <bdi dir="ltr">{formatDate(s.dueDate, fmt)}</bdi>
                  </td>
                  <td className="py-1">{formatMoney(s.amount, fmt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {record.returnReason ? <Alert tone="err" title={E.returnReason}>{record.returnReason}</Alert> : null}
      {record.status === "pending_verification" && record.recordedByMe ? <p className="m-0 text-13 text-muted">{E.sameMemberNote}</p> : null}
      {record.status === "pending_verification" && canVerify && !record.recordedByMe ? <VerifyPanel record={record} base={base} sourceVersionId={sourceVersionId} /> : null}
    </li>
  );
}

function VerifyPanel({ record, base, sourceVersionId }: { record: ExecutionRecord; base: string; sourceVersionId: string | null }) {
  const c = useTeamCopy();
  const E = c.execution;
  const router = useRouter();
  const key = useIdempotencyKey();
  const [checked, setChecked] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stepUp, setStepUp] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);
  const act = useTeamAction();
  const [reason, setReason] = useState("");

  const publish = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `${base}/execution/records/${record.id}/verify`, { decision: "publish", checklist: checked }, { idempotencyKey: key.get() });
      key.reset();
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
      <h3 className="m-0 text-16 font-bold">{E.verifyPanel}</h3>
      {sourceVersionId ? (
        <a href={`/api${base}/documents/${sourceVersionId}/file`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 self-start text-14 font-semibold">
          <Icon name="open_in_new" size={18} />
          {E.openSource}
        </a>
      ) : null}
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">{c.verify.checklist}</legend>
        {CHECKLIST.map((i) => (
          <Checkbox key={i} label={E.checklist[i]} checked={checked.includes(i)} onChange={(e) => setChecked((x) => (e.target.checked ? [...x, i] : x.filter((y) => y !== i)))} />
        ))}
      </fieldset>
      {error ? <Alert tone="err">{error}</Alert> : null}
      <div className="flex flex-wrap gap-2">
        <Button loading={busy} softDisabled={checked.length !== CHECKLIST.length} onClick={() => void publish()} icon="verified">
          {c.verify.publish}
        </Button>
        <Button variant="secondary" onClick={() => setReturnOpen(true)}>
          {c.verify.return}
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
      <Dialog
        open={returnOpen}
        onClose={() => setReturnOpen(false)}
        title={E.returnTitle}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setReturnOpen(false)}>
              {c.cancel}
            </Button>
            <Button
              loading={act.busy}
              onClick={async () => (await act.run("POST", `${base}/execution/records/${record.id}/verify`, { decision: "return", reason }, undefined, "/team/verify")) && setReturnOpen(false)}
            >
              {c.send}
            </Button>
          </div>
        }
      >
        <Textarea label={c.verify.reason} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} error={act.fieldErrors.reason} />
        {act.error && !act.fieldErrors.reason ? <Alert tone="err">{act.error}</Alert> : null}
      </Dialog>
    </div>
  );
}

function ReportItem({ report, base, proofVersionId, canAct, onNote, onConfirm }: {
  report: PaymentReport; base: string; proofVersionId: string | null; canAct: boolean; onNote: () => void; onConfirm: () => void;
}) {
  const c = useTeamCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const open = report.status !== "confirmed_by_lender";
  return (
    <li className="flex flex-col gap-1.5 rounded-md border border-line p-3 text-14">
      <span className="flex flex-wrap items-center gap-2">
        <bdi dir="ltr" className="font-mono text-13">
          {report.reference}
        </bdi>
        <strong>{formatMoney(report.amount, fmt)}</strong>
        <Tag tone={report.status === "confirmed_by_lender" ? "ok" : "warn"}>{E.reportStatus[report.status]}</Tag>
        {report.scheduleItemNo !== null ? <span className="text-13 text-muted">{E.installmentNo(report.scheduleItemNo)}</span> : null}
      </span>
      <span className="text-13 text-muted">
        {E.transferDate}: <bdi dir="ltr">{formatDate(report.transferDate, fmt)}</bdi>
        {report.bankReference ? (
          <>
            {" "}· {E.bankReference}: <bdi dir="ltr">{report.bankReference}</bdi>
          </>
        ) : null}
      </span>
      {proofVersionId ? (
        <a href={`/api${base}/documents/${proofVersionId}/file`} className="inline-flex items-center gap-1 self-start text-13 font-semibold">
          <Icon name="receipt_long" size={16} />
          {E.proof}
        </a>
      ) : null}
      {report.teamNote ? (
        <p className="m-0 rounded-sm bg-info-bg p-2 text-13">
          {E.teamNote}: {report.teamNote}
        </p>
      ) : null}
      {open && canAct ? (
        <div className="flex flex-wrap gap-2">
          <Button size="sm" onClick={onConfirm}>
            {E.confirm}
          </Button>
          <Button size="sm" variant="secondary" onClick={onNote}>
            {E.note}
          </Button>
        </div>
      ) : null}
    </li>
  );
}

function NoteDialog({ report, onClose, base }: { report: PaymentReport | null; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const E = c.execution;
  const [text, setText] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={report !== null}
      onClose={onClose}
      title={E.noteTitle}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={async () => report && (await act.run("POST", `${base}/payment-reports/${report.id}/note`, { text })) && onClose()}>
            {c.save}
          </Button>
        </div>
      }
    >
      <Textarea label={E.noteText} help={E.noteHelp} rows={3} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} error={act.fieldErrors.text} />
      {act.error && !act.fieldErrors.text ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

/** Parses «no, YYYY-MM-DD, amount» lines; returns the rows and the 1-based numbers of invalid lines. */
function parseSchedule(text: string) {
  const rows: Array<{ no: number; dueDate: string; amount: number }> = [];
  const bad: number[] = [];
  text.split("\n").forEach((line, i) => {
    const t = line.trim();
    if (!t) return;
    const m = /^(\d+)\s*[,،;\t ]\s*(\d{4}-\d{2}-\d{2})\s*[,،;\t ]\s*([\d.,]+)$/.exec(t);
    if (!m) return void bad.push(i + 1);
    rows.push({ no: Number(m[1]), dueDate: m[2], amount: Number(m[3].replace(/,/g, "")) });
  });
  return { rows, bad };
}

function RecordDrawer({ draft, onClose, base, detail }: { draft: Draft; onClose: () => void; base: string; detail: TeamRequestDetail }) {
  const c = useTeamCopy();
  const E = c.execution;
  const kind = draft.kind;
  const letters = detail.documents.filter((d) => d.kind === "lender_letter");
  const accepted = detail.offers.find((o) => o.status === "published" || o.status === "superseded");
  const published = detail.execution.records.filter((r) => r.kind === kind && r.status === "published");
  const openReports = detail.execution.paymentReports.filter((p) => p.status !== "confirmed_by_lender");
  const [sourceId, setSourceId] = useState("");
  const [lenderReference, setLenderReference] = useState("");
  const [lenderDate, setLenderDate] = useState("");
  const [summaryText, setSummaryText] = useState("");
  const [explanationText, setExplanationText] = useState("");
  const [shareSource, setShareSource] = useState(true);
  const [activationDate, setActivationDate] = useState("");
  const [newInstallment, setNewInstallment] = useState<number | null>(accepted?.newInstallment ?? null);
  const [termMonths, setTermMonths] = useState(accepted?.termMonths ? String(accepted.termMonths) : "");
  const [settlementAmount, setSettlementAmount] = useState<number | null>(accepted?.settlementAmount ?? null);
  const [termsText, setTermsText] = useState("");
  const [scheduleText, setScheduleText] = useState("");
  const [amount, setAmount] = useState<number | null>(draft.amount ?? null);
  const [receivedOn, setReceivedOn] = useState("");
  const [scheduleItemNo, setScheduleItemNo] = useState(draft.scheduleItemNo ? String(draft.scheduleItemNo) : "");
  const [answersReportId, setAnswersReportId] = useState(draft.answersReportId ?? "");
  const [noticeCategory, setNoticeCategory] = useState("missed_installment");
  const [documentKind, setDocumentKind] = useState(detail.execution.relevantClosureKinds[0] ?? "clearance");
  const [supersedesId, setSupersedesId] = useState("");
  const [correctionReason, setCorrectionReason] = useState("");
  const act = useTeamAction();
  const fe = act.fieldErrors;
  const schedule = parseSchedule(scheduleText);

  const submit = async () => {
    if (schedule.bad.length > 0) return;
    const ok = await act.run("POST", `${base}/execution/records`, {
      kind,
      sourceDocumentId: sourceId || null,
      lenderReference,
      lenderDate: lenderDate || null,
      summaryText,
      explanationText,
      shareSource,
      ...(kind === "agreement"
        ? { activationDate: activationDate || null, newInstallment, termMonths: termMonths ? Number(termMonths) : null, settlementAmount, termsText: termsText || null, schedule: schedule.rows }
        : {}),
      ...(kind === "payment_confirmation"
        ? { amount, receivedOn: receivedOn || null, scheduleItemNo: scheduleItemNo ? Number(scheduleItemNo) : null, answersReportId: answersReportId || null }
        : {}),
      ...(kind === "lender_notice" ? { noticeCategory } : {}),
      ...(kind === "closure_document" ? { documentKind } : {}),
      supersedesRecordId: supersedesId || null,
      correctionReason: supersedesId ? correctionReason : null,
    });
    if (ok) onClose();
  };

  return (
    <Drawer
      open
      onClose={onClose}
      title={E.recordTitle(kind)}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={() => void submit()}>
            {E.submit}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        <Select
          label={E.source}
          placeholder={E.sourcePick}
          value={sourceId}
          onChange={(e) => setSourceId(e.target.value)}
          options={letters.map((d) => ({ value: d.id, label: `${d.name}${d.fileName ? ` · ${d.fileName}` : ""}` }))}
          help={E.sourceHelp}
          error={fe.sourceDocumentId}
        />
        <div className="grid gap-3 sm:grid-cols-2">
          <TextField label={E.lenderReference} ltr mono maxLength={100} value={lenderReference} onChange={(e) => setLenderReference(e.target.value)} error={fe.lenderReference} />
          <TextField label={E.lenderDate} type="date" ltr value={lenderDate} onChange={(e) => setLenderDate(e.target.value)} error={fe.lenderDate} />
        </div>

        {kind === "agreement" ? (
          <>
            <p className="m-0 text-13 text-muted">{E.prefilled}</p>
            <TextField label={E.activationDate} type="date" ltr value={activationDate} onChange={(e) => setActivationDate(e.target.value)} />
            {detail.execution.path === "p2" ? (
              <AmountField label={E.settlementAmount} value={settlementAmount} onValueChange={setSettlementAmount} error={fe.settlementAmount} />
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <AmountField label={E.newInstallment} value={newInstallment} onValueChange={setNewInstallment} error={fe.newInstallment} />
                <TextField label={E.termMonths} inputMode="numeric" ltr value={termMonths} onChange={(e) => setTermMonths(e.target.value.replace(/\D/g, ""))} error={fe.termMonths} />
              </div>
            )}
            <Textarea label={E.termsText} rows={2} maxLength={2000} value={termsText} onChange={(e) => setTermsText(e.target.value)} />
            <Textarea
              label={E.schedule}
              help={schedule.rows.length > 0 ? `${E.scheduleHelp} · ${E.scheduleRows(schedule.rows.length)}` : E.scheduleHelp}
              rows={5}
              dir="ltr"
              value={scheduleText}
              onChange={(e) => setScheduleText(e.target.value)}
              error={schedule.bad.length > 0 ? `${E.scheduleInvalid}: ${schedule.bad.join("، ")}` : fe.schedule}
            />
          </>
        ) : null}

        {kind === "payment_confirmation" ? (
          <>
            <div className="grid gap-3 sm:grid-cols-2">
              <AmountField label={E.amount} value={amount} onValueChange={setAmount} error={fe.amount} />
              <TextField label={E.receivedOn} type="date" ltr value={receivedOn} onChange={(e) => setReceivedOn(e.target.value)} error={fe.receivedOn} />
            </div>
            <TextField label={E.scheduleItemNo} inputMode="numeric" ltr value={scheduleItemNo} onChange={(e) => setScheduleItemNo(e.target.value.replace(/\D/g, ""))} />
            <Select
              label={E.answersReport}
              placeholder="—"
              value={answersReportId}
              onChange={(e) => setAnswersReportId(e.target.value)}
              options={openReports.map((p) => ({ value: p.id, label: p.reference }))}
              error={fe.answersReportId}
            />
          </>
        ) : null}

        {kind === "lender_notice" ? (
          <Select label={E.noticeCategory} value={noticeCategory} onChange={(e) => setNoticeCategory(e.target.value)} options={Object.entries(E.noticeCategories).map(([value, label]) => ({ value, label }))} error={fe.noticeCategory} />
        ) : null}
        {kind === "closure_document" ? (
          <Select label={E.documentKind} value={documentKind} onChange={(e) => setDocumentKind(e.target.value)} options={Object.entries(E.documentKinds).map(([value, label]) => ({ value, label }))} error={fe.documentKind} />
        ) : null}

        <Textarea label={E.summaryText} rows={3} maxLength={2000} value={summaryText} onChange={(e) => setSummaryText(e.target.value)} error={fe.summaryText} />
        <Textarea label={E.explanationText} rows={3} maxLength={2000} value={explanationText} onChange={(e) => setExplanationText(e.target.value)} error={fe.explanationText} />
        <Checkbox label={E.shareSource} checked={shareSource} onChange={(e) => setShareSource(e.target.checked)} />
        {published.length > 0 ? (
          <>
            <Select
              label={E.correction}
              placeholder="—"
              value={supersedesId}
              onChange={(e) => setSupersedesId(e.target.value)}
              options={published.map((r) => ({ value: r.id, label: `${E.kinds[r.kind]} · ${r.lenderReference}` }))}
              error={fe.supersedesRecordId}
            />
            {supersedesId ? <Textarea label={E.correctionReason} rows={2} value={correctionReason} onChange={(e) => setCorrectionReason(e.target.value)} error={fe.correctionReason} /> : null}
          </>
        ) : null}
        <p className="m-0 text-13 text-muted">{E.sameMemberNote}</p>
        {act.error && Object.keys(fe).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

/** Q17: back to coordination from execution tracking, with a reason the individual sees. */
export function ContinueFromTrackingDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const E = c.execution;
  const [reason, setReason] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={E.continueTitle}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            {c.cancel}
          </Button>
          <Button loading={act.busy} onClick={async () => (await act.run("POST", `${base}/continue`, { nextStep: null, reason })) && onClose()}>
            {c.review.actionKeys.continue_coordination}
          </Button>
        </div>
      }
    >
      <Textarea label={E.continueReason} help={E.continueHelp} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} error={act.fieldErrors.reason} />
      {act.error && !act.fieldErrors.reason ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}
