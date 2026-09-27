"use client";

import Link from "next/link";
import { IndividualFrame, Panel, useRequestCopy } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import type { MyExecution, MyRequestDetail } from "@/lib/api/requests";
import { cn } from "@/lib/cn";
import { formatDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

/*
 * Phase 1A-2 step 3 (design request D-8, «بانتظار اعتماد التصميم»): the individual follows the execution of their
 * agreement. Every fact names its source (the lender's document or their own report). Due dates are the lender's
 * schedule — no countdown, no overdue styling, no reminders (Q6/Q12, Q16). «رهون لا تستلم أي مبالغ» is always shown.
 */

const fileUrl = (reference: string, versionId: string) => `/api/my/requests/${encodeURIComponent(reference)}/documents/${versionId}/file`;

function SourceLine({ reference, date, versionId, requestRef }: { reference: string; date: string; versionId: string | null; requestRef: string }) {
  const c = useRequestCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  return (
    <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-13 text-muted">
      <span>
        {E.sourceOn} <bdi dir="ltr">{formatDate(date, { locale, numerals })}</bdi> · <bdi dir="ltr" className="whitespace-nowrap">{reference}</bdi>
      </span>
      {versionId ? (
        <a href={fileUrl(requestRef, versionId)} className="inline-flex min-h-11 items-center gap-1 font-semibold">
          <Icon name="description" size={16} />
          {E.openSource}
        </a>
      ) : null}
    </span>
  );
}

function NoFunds({ text }: { text: string }) {
  return (
    <p className="m-0 flex items-start gap-2 rounded-md bg-subtle p-3 text-15 leading-6">
      <Icon name="account_balance" size={20} className="mt-0.5 flex-none text-muted" />
      {text}
    </p>
  );
}

function ClosureDocuments({ execution, requestRef }: { execution: MyExecution; requestRef: string }) {
  const c = useRequestCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  if (execution.closureDocuments.length === 0) return null;
  return (
    <Panel aria-labelledby="closure-h">
      <h2 id="closure-h" className="m-0 flex items-center gap-2 text-18 font-bold">
        <Icon name="task_alt" size={22} className="text-ok" />
        {E.closureTitle}
      </h2>
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {execution.closureDocuments.map((d) => (
          <li key={d.lenderReference} className="flex flex-col gap-1 rounded-md border border-line p-3">
            <strong className="text-16">{E.closureKinds[d.documentKind ?? "other"] ?? E.closureKinds.other}</strong>
            <p className="m-0 text-15 leading-6">{d.explanationText}</p>
            <span className="text-13 text-muted">
              {E.sourceOn} <bdi dir="ltr">{formatDate(d.lenderDate, { locale, numerals })}</bdi> · <bdi dir="ltr" className="whitespace-nowrap">{d.lenderReference}</bdi>
            </span>
            {d.sourceVersionId ? (
              <Button href={fileUrl(requestRef, d.sourceVersionId)} hardNavigation variant="secondary" size="lg" icon="download" className="self-start">
                {E.download}
              </Button>
            ) : null}
          </li>
        ))}
      </ul>
      <p className="m-0 text-13 text-muted">{E.closureKeep}</p>
    </Panel>
  );
}

function Notices({ execution, requestRef }: { execution: MyExecution; requestRef: string }) {
  const c = useRequestCopy();
  const E = c.execution;
  const ref = encodeURIComponent(requestRef);
  if (execution.notices.length === 0) return null;
  return (
    <Panel aria-labelledby="notices-h">
      <h2 id="notices-h" className="m-0 text-18 font-bold">
        {E.noticesTitle}
      </h2>
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {execution.notices.map((n) => (
          <li key={n.lenderReference} className="flex flex-col gap-1.5 rounded-md border border-line p-3">
            <strong className="text-16">{E.noticeCategories[n.category ?? "other"] ?? E.noticeCategories.other}</strong>
            <p className="m-0 text-15 leading-6">{n.explanationText}</p>
            <p className="m-0 text-14 font-semibold text-ok">{n.noActionNote}</p>
            <SourceLine reference={n.lenderReference} date={n.lenderDate} versionId={n.sourceVersionId} requestRef={requestRef} />
          </li>
        ))}
      </ul>
      <p className="m-0 text-14 leading-6 text-charcoal">{E.noticeHelp}</p>
      <div className="flex flex-wrap gap-2">
        <Button href={`/my/requests/${ref}/messages`} variant="secondary" size="lg" icon="chat">
          {c.messages.open}
        </Button>
        <Button href={`/my/requests/${ref}/concern?kind=objection`} variant="text" size="lg" icon="report">
          {c.concern.objection}
        </Button>
      </div>
    </Panel>
  );
}

/** E01 on the tracker: the agreement in one line, the latest confirmation, notices and closure documents. */
export function ExecutionSummary({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const x = detail.execution;
  if (!x) return null;
  const ref = encodeURIComponent(detail.reference);
  const a = x.agreement;
  const last = x.confirmations[0];
  return (
    <>
      <Panel aria-labelledby="exec-h">
        <h2 id="exec-h" className="m-0 text-18 font-bold">
          {E.title}
        </h2>
        <p className="m-0 text-15 leading-6 text-charcoal">{E.lead}</p>
        <NoFunds text={x.noFundsNote} />
        <div className="flex flex-col gap-1 rounded-md border border-line p-3">
          <strong className="text-15">{E.agreementTitle}</strong>
          {a ? (
            <>
              <span className="text-16">
                {a.newInstallment !== null ? `${E.installment}: ${formatMoney(a.newInstallment, fmt)}` : null}
                {a.settlementAmount !== null ? `${E.settlement}: ${formatMoney(a.settlementAmount, fmt)}` : null}
                {a.termMonths !== null ? ` · ${E.months(a.termMonths)}` : null}
              </span>
              <SourceLine reference={a.lenderReference} date={a.lenderDate} versionId={a.sourceVersionId} requestRef={detail.reference} />
            </>
          ) : (
            <span className="text-15 text-muted">{E.noAgreement}</span>
          )}
        </div>
        {last ? (
          <div className="flex flex-col gap-1 rounded-md border border-ok-line bg-ok-bg p-3">
            <strong className="text-15">{E.lastConfirmed}</strong>
            <span className="text-16">
              {last.amount !== null ? formatMoney(last.amount, fmt) : null}
              {last.scheduleItemNo !== null ? ` · ${E.installmentNo(last.scheduleItemNo)}` : null}
            </span>
            {last.receivedOn ? (
              <span className="text-13 text-muted">
                {E.receivedOn} <bdi dir="ltr">{formatDate(last.receivedOn, fmt)}</bdi>
              </span>
            ) : null}
          </div>
        ) : null}
        <div className="flex flex-wrap gap-2">
          <Button href={`/my/requests/${ref}/execution`} variant="secondary" size="lg" icon="receipt_long">
            {E.details}
          </Button>
          {detail.canReportPayment ? (
            <Button href={`/my/requests/${ref}/payment`} size="lg" icon="add">
              {E.report}
            </Button>
          ) : null}
        </div>
      </Panel>
      <Notices execution={x} requestRef={detail.reference} />
      <ClosureDocuments execution={x} requestRef={detail.reference} />
    </>
  );
}

/** E02 + E04 + E05: the agreement and the lender's schedule, what the lender confirmed, the individual's reports. */
export function ExecutionDetail({ detail, reported = false }: { detail: MyRequestDetail; reported?: boolean }) {
  const c = useRequestCopy();
  const E = c.execution;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const x = detail.execution!;
  const a = x.agreement;
  const ref = encodeURIComponent(detail.reference);
  return (
    <IndividualFrame title={E.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}` }} width="wide">
      <header className="flex flex-col gap-2">
        <h1 className="m-0 text-24 leading-9 font-bold">{E.title}</h1>
        <p className="m-0 text-16 leading-7 text-charcoal">{E.lead}</p>
        {reported ? (
          <Alert tone="ok" role="status">
            {E.payment.done}
          </Alert>
        ) : null}
        <NoFunds text={x.noFundsNote} />
        {detail.canReportPayment ? (
          <Button href={`/my/requests/${ref}/payment`} size="xl" icon="add" className="self-start">
            {E.report}
          </Button>
        ) : null}
      </header>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)] lg:gap-6">
        <div className="flex flex-col gap-4">
          <Panel aria-labelledby="agreement-h">
            <h2 id="agreement-h" className="m-0 text-18 font-bold">
              {E.agreementTitle}
            </h2>
            {a ? (
              <>
                <KeyValueList
                  rows={[
                    ...(a.newInstallment !== null ? [{ key: E.installment, value: formatMoney(a.newInstallment, fmt) }] : []),
                    ...(a.termMonths !== null ? [{ key: E.term, value: E.months(a.termMonths) }] : []),
                    ...(a.settlementAmount !== null ? [{ key: E.settlement, value: formatMoney(a.settlementAmount, fmt) }] : []),
                    ...(a.activationDate ? [{ key: E.activation, value: <bdi dir="ltr">{formatDate(a.activationDate, fmt)}</bdi> }] : []),
                    ...(a.termsText ? [{ key: E.terms, value: a.termsText }] : []),
                  ]}
                />
                <div className="flex flex-col gap-1 rounded-md bg-subtle p-3">
                  <strong className="text-15">{E.whatItMeans}</strong>
                  <p className="m-0 text-15 leading-6">{a.explanationText}</p>
                </div>
                <SourceLine reference={a.lenderReference} date={a.lenderDate} versionId={a.sourceVersionId} requestRef={detail.reference} />
              </>
            ) : (
              <p className="m-0 text-15 text-muted">{E.noAgreement}</p>
            )}
          </Panel>

          {x.schedule.length > 0 ? (
            <Panel aria-labelledby="schedule-h">
              <h2 id="schedule-h" className="m-0 text-18 font-bold">
                {E.scheduleTitle}
              </h2>
              <p className="m-0 text-14 leading-6 text-charcoal">{E.scheduleNote}</p>
              <ul className="m-0 flex list-none flex-col divide-y divide-divider p-0">
                {x.schedule.map((s) => (
                  <li key={s.no} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2.5">
                    <span className="flex flex-col">
                      <strong className="text-15">{E.installmentNo(s.no)}</strong>
                      <bdi dir="ltr" className="self-start text-13 text-muted">
                        {formatDate(s.dueDate, fmt)}
                      </bdi>
                    </span>
                    <span className="flex flex-col items-end gap-0.5">
                      <span className="text-15 font-semibold">{formatMoney(s.amount, fmt)}</span>
                      {s.state ? (
                        <span className={cn("text-13 font-semibold", s.state === "confirmed_by_lender" ? "text-ok" : "text-charcoal")}>{E.rowStates[s.state]}</span>
                      ) : null}
                    </span>
                  </li>
                ))}
              </ul>
            </Panel>
          ) : null}

          <Notices execution={x} requestRef={detail.reference} />
        </div>

        <aside className="flex flex-col gap-4">
          <Panel aria-labelledby="confirmed-h">
            <h2 id="confirmed-h" className="m-0 text-17 font-bold">
              {E.confirmationsTitle}
            </h2>
            {x.confirmations.length === 0 ? <p className="m-0 text-15 text-muted">{E.noConfirmations}</p> : null}
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
              {x.confirmations.map((k) => (
                <li key={k.lenderReference} className="flex flex-col gap-1 rounded-md border border-ok-line bg-ok-bg p-3">
                  <strong className="text-16">
                    {k.amount !== null ? formatMoney(k.amount, fmt) : null}
                    {k.scheduleItemNo !== null ? ` · ${E.installmentNo(k.scheduleItemNo)}` : null}
                  </strong>
                  {k.receivedOn ? (
                    <span className="text-14">
                      {E.receivedOn} <bdi dir="ltr">{formatDate(k.receivedOn, fmt)}</bdi>
                    </span>
                  ) : null}
                  <p className="m-0 text-14 leading-6">{k.explanationText}</p>
                  <SourceLine reference={k.lenderReference} date={k.lenderDate} versionId={k.sourceVersionId} requestRef={detail.reference} />
                </li>
              ))}
            </ul>
          </Panel>

          {x.paymentReports.length > 0 ? (
            <Panel aria-labelledby="reports-h">
              <h2 id="reports-h" className="m-0 text-17 font-bold">
                {E.reportsTitle}
              </h2>
              <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
                {x.paymentReports.map((p) => (
                  <li key={p.reference} className="flex flex-col gap-1 rounded-md border border-line p-3">
                    <span className="flex flex-wrap items-center gap-2">
                      <strong className="text-16">{formatMoney(p.amount, fmt)}</strong>
                      {p.scheduleItemNo !== null ? <span className="text-14 text-muted">{E.installmentNo(p.scheduleItemNo)}</span> : null}
                    </span>
                    <span className={cn("text-14 font-semibold", p.status === "confirmed_by_lender" ? "text-ok" : "text-charcoal")}>{E.reportStatus[p.status]}</span>
                    <span className="text-13 text-muted">
                      {E.transferredOn} <bdi dir="ltr">{formatDate(p.transferDate, fmt)}</bdi> · <bdi dir="ltr">{p.reference}</bdi>
                    </span>
                    {p.teamNote ? (
                      <p className="m-0 rounded-sm bg-info-bg p-2 text-14 leading-6">
                        <strong>{E.teamNote}: </strong>
                        {p.teamNote}
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>
            </Panel>
          ) : null}

          <ClosureDocuments execution={x} requestRef={detail.reference} />
          <Link href={`/my/requests/${ref}`} className="inline-flex min-h-11 items-center gap-1 self-start text-15 font-semibold">
            <Icon name="arrow_forward" size={20} mirror />
            {c.tracker.eyebrow}
          </Link>
        </aside>
      </div>
    </IndividualFrame>
  );
}

/** A notice shown on the withdraw screen during tracking (E06, V4 proposed text from the server). */
export function WithdrawEffect({ text }: { text: string | null }) {
  if (!text) return null;
  return <Alert tone="info">{text}</Alert>;
}
