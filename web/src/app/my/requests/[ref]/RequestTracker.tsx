"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { likelyPaths } from "@/components/individual/copy";
import { IndividualFrame, Panel, RequestStatusChip, useRequestCopy, useRequestSubmit, WaitingOnLine } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { Tag } from "@/components/ui/Status";
import { apiSend } from "@/lib/api/client";
import type { MyRequestDetail } from "@/lib/api/requests";
import { cn } from "@/lib/cn";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";


/**
 * D-3 request home. Every state shows the status label, «ننتظر» and the next step — never a date in the future (Q6).
 * «ماذا ستفعل رهون لك» lists the likely paths from the individual's preference, labelled provisional (Q11: help
 * paths, not outcomes). Mobile stacks; from 1024px a main column and an aside.
 */
export function RequestTracker({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const T = c.tracker;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const ref = encodeURIComponent(detail.reference);
  const terminal = detail.status === "closed" || detail.status === "not_eligible" || detail.status === "withdrawn";
  const nextText = detail.nextStepText ?? c.nextStep[detail.status];
  const paths = likelyPaths(detail.fields.pathPreference);
  const f = detail.fields;

  return (
    <IndividualFrame title={T.eyebrow} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: "/my" }} width="wide">
      <header className="flex flex-col gap-2">
        <span className="text-14 text-muted">
          {T.eyebrow} ·{" "}
          <bdi dir="ltr" className="font-mono">
            {detail.reference}
          </bdi>
        </span>
        <h1 className="m-0 text-24 leading-9 font-bold lg:text-28 lg:leading-10">{detail.institutionName}</h1>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <RequestStatusChip status={detail.status} />
          <WaitingOnLine waitingOn={detail.waitingOn} />
        </div>
        <span className="text-13 text-muted">
          {detail.submittedAt ? T.submittedOn : T.startedOn} <bdi dir="ltr">{formatDate(detail.submittedAt ?? detail.createdAt, fmt)}</bdi>
        </span>
      </header>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)] lg:gap-6">
        <div className="flex flex-col gap-4">
          {/* Next step — plain language, no date (Q6). */}
          <section aria-labelledby="next-h" className="flex flex-col gap-2.5 rounded-[12px] border border-line border-t-4 border-t-orange bg-white p-[18px] lg:p-6">
            <h2 id="next-h" className="m-0 text-14 font-bold text-muted">
              {T.nextStep}
            </h2>
            <p className="m-0 text-19 leading-8 font-semibold">{nextText}</p>
            {detail.status === "offer_available" && detail.offer ? (
              <Button href={`/my/requests/${ref}/offer`} size="xl" className="self-start" icon="local_offer">
                {c.offer.review}
              </Button>
            ) : null}
            {detail.status === "info_requested" && detail.canAddInfo ? (
              <Button href={detail.consent ? `/my/requests/${ref}/add` : `/my/requests/${ref}/consent`} size="xl" className="self-start">
                {detail.consent ? T.addInfo : T.renewConsent}
              </Button>
            ) : null}
          </section>

          {detail.status === "not_eligible" && detail.notEligibleReason ? (
            <Alert tone="warn" title={T.reason}>
              {detail.notEligibleReason}
            </Alert>
          ) : null}
          {detail.status === "closed" && detail.outcome?.summary ? (
            <Alert tone="info" title={(detail.outcome.code && c.outcome[detail.outcome.code]) || T.outcome}>
              {detail.outcome.summary}
            </Alert>
          ) : null}

          {detail.responses.length > 0 ? (
            <Panel aria-labelledby="responses-h">
              <h2 id="responses-h" className="m-0 text-18 font-bold">
                {c.offer.yourResponses}
              </h2>
              <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
                {detail.responses.map((x) => (
                  <li key={x.reference} className="flex flex-col gap-1 rounded-md border border-line p-3">
                    <strong className="text-15">{c.offer.responseKinds[x.kind]}</strong>
                    {x.text ? <p className="m-0 text-15 leading-6 whitespace-pre-line">{x.text}</p> : null}
                    <span className="text-13 text-muted">
                      <bdi dir="ltr">{x.reference}</bdi> · <bdi dir="ltr">{formatDateTime(x.at, fmt)}</bdi>
                    </span>
                    <span className={cn("text-13 font-semibold", x.relayed ? "text-ok" : "text-muted")}>{x.relayed ? c.offer.relayed : c.offer.notRelayed}</span>
                  </li>
                ))}
              </ul>
              {detail.offer ? (
                <Link href={`/my/requests/${ref}/offer`} className="inline-flex min-h-11 items-center self-start text-15 font-semibold">
                  {c.offer.title}
                </Link>
              ) : null}
            </Panel>
          ) : null}

          {/* «ماذا ستفعل رهون لك» */}
          <Panel aria-labelledby="rahoon-h">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 id="rahoon-h" className="m-0 text-18 font-bold">
                {T.whatRahoon}
              </h2>
              {!terminal ? (
                <Tag tone="warn" icon="schedule">
                  {T.provisional}
                </Tag>
              ) : null}
            </div>
            <div className="flex flex-col gap-1 rounded-md bg-subtle p-3">
              <strong className="text-15">{T.doingNow}</strong>
              <p className="m-0 text-15 leading-6">{c.doingNow[detail.status]}</p>
            </div>
            {!terminal ? (
              <>
                <h3 className="m-0 text-15 font-semibold text-muted">{T.likelyPaths}</h3>
                <ul className="m-0 flex list-none flex-col gap-2 p-0">
                  {paths.map((p) => (
                    <li key={p} className="flex flex-col gap-0.5 rounded-md border border-line p-3">
                      <strong className="text-16">{c.paths[p].title}</strong>
                      <span className="text-15 leading-6 text-charcoal">{c.paths[p].body}</span>
                    </li>
                  ))}
                </ul>
              </>
            ) : null}
            <Link href={`/my/requests/${ref}/paths`} className="inline-flex min-h-11 items-center gap-1 self-start text-15 font-semibold">
              {T.allPaths}
              <Icon name="chevron_left" size={20} mirror />
            </Link>
            <p className="m-0 text-13 leading-5 text-muted">{c.paths.footnote}</p>
          </Panel>

          {/* Timeline — past events only. */}
          <Panel aria-labelledby="timeline-h">
            <h2 id="timeline-h" className="m-0 text-18 font-bold">
              {T.timeline}
            </h2>
            <ol className="m-0 flex list-none flex-col p-0">
              {detail.timeline.map((e, i) => (
                <li key={`${e.at}-${i}`} className="relative flex gap-3 pb-4 last:pb-0">
                  <span aria-hidden="true" className="relative flex w-5 flex-none justify-center">
                    <span className={cn("mt-1.5 size-2.5 rounded-full", i === 0 ? "bg-orange" : "bg-charcoal")} />
                    {i < detail.timeline.length - 1 ? <span className="absolute top-5 bottom-0 w-px bg-line" /> : null}
                  </span>
                  <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <strong className="text-15 leading-6">{e.title}</strong>
                    {e.body ? <p className="m-0 text-14 leading-6 whitespace-pre-line text-charcoal">{e.body}</p> : null}
                    <span className="text-13 text-muted">
                      <bdi dir="ltr">{formatDateTime(e.at, fmt)}</bdi>
                    </span>
                  </div>
                </li>
              ))}
            </ol>
          </Panel>
        </div>

        <aside className="flex flex-col gap-4">
          <ConsentPanel detail={detail} terminal={terminal} />

          <Panel aria-labelledby="docs-h">
            <h2 id="docs-h" className="m-0 text-17 font-bold">
              {T.documents}
            </h2>
            {detail.documents.length === 0 ? (
              <p className="m-0 text-15 text-muted">{c.wizard.review.noDocuments}</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {detail.documents.map((d) => (
                  <li key={d.id} className="flex items-center gap-2.5">
                    <Icon name="description" size={22} className="text-muted" />
                    <div className="flex min-w-0 flex-1 flex-col">
                      {d.versionId ? (
                        <a href={`/api/my/requests/${ref}/documents/${d.versionId}/file`} className="truncate text-15 font-semibold">
                          {d.name}
                        </a>
                      ) : (
                        <span className="text-15">{d.name}</span>
                      )}
                      <span className="text-13 text-muted">
                        {d.uploadedAt ? <bdi dir="ltr">{formatDate(d.uploadedAt, fmt)}</bdi> : null}
                        {d.addedAfterSubmit ? ` · ${T.addedLater}` : null}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          <details className="group rounded-[12px] border border-line bg-white px-4 py-3">
            <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-2 text-16 font-semibold [&::-webkit-details-marker]:hidden">
              {T.details}
              <Icon name="expand_more" size={22} className="transition-transform group-open:rotate-180" />
            </summary>
            <KeyValueList
              rows={[
                { key: c.wizard.review.name, value: f.applicantFullName ?? "—" },
                { key: c.wizard.review.contract, value: f.contractNumber ? <bdi dir="ltr" className="font-mono">{f.contractNumber}</bdi> : "—" },
                { key: c.wizard.review.installment, value: f.monthlyInstallment === null ? "—" : formatMoney(f.monthlyInstallment, fmt) },
                { key: c.wizard.review.arrears, value: f.arrearsDuration ? c.wizard.finance.arrearsOptions[f.arrearsDuration] : "—" },
                { key: c.wizard.review.city, value: f.propertyCity ?? "—" },
                { key: c.wizard.review.preference, value: f.pathPreference ? c.wizard.situation.options[f.pathPreference] : "—" },
                ...(f.situationText ? [{ key: c.wizard.review.situation, value: f.situationText }] : []),
              ]}
            />
          </details>

          {detail.status !== "draft" ? (
            <Button href={`/my/requests/${ref}/messages`} variant="secondary" size="xl" fullWidth icon="chat">
              {c.messages.open}
            </Button>
          ) : null}
          {detail.canAddInfo || detail.canWithdraw ? (
            <div className="flex flex-col gap-2.5">
              {detail.canAddInfo && detail.status !== "info_requested" ? (
                <Button href={`/my/requests/${ref}/add`} variant="secondary" size="xl" fullWidth icon="add">
                  {T.addInfo}
                </Button>
              ) : null}
              {detail.canWithdraw ? (
                <Button href={`/my/requests/${ref}/withdraw`} variant="text" size="lg" className="self-center text-15">
                  {T.withdraw}
                </Button>
              ) : null}
            </div>
          ) : null}
          {terminal ? (
            <Link href="/my" className="inline-flex min-h-12 items-center gap-1.5 self-start text-15 font-semibold">
              <Icon name="add" size={20} />
              {T.newRequest}
            </Link>
          ) : null}
        </aside>
      </div>
    </IndividualFrame>
  );
}

function ConsentPanel({ detail, terminal }: { detail: MyRequestDetail; terminal: boolean }) {
  const c = useRequestCopy();
  const T = c.tracker;
  const { numerals } = useI18n();
  const router = useRouter();
  const [confirming, setConfirming] = useState(false);
  const withdraw = useRequestSubmit();
  const ref = encodeURIComponent(detail.reference);

  const doWithdraw = async () => {
    const res = await withdraw.run((key) => apiSend("POST", `/my/requests/${ref}/consent/withdraw`, undefined, { idempotencyKey: key }));
    if (res.ok) {
      setConfirming(false);
      router.refresh();
    }
  };

  return (
    <Panel aria-labelledby="consent-h">
      <h2 id="consent-h" className="m-0 flex items-center gap-2 text-17 font-bold">
        <Icon name={detail.consent ? "verified_user" : "gpp_maybe"} size={22} className={detail.consent ? "text-ok" : "text-warn"} />
        {T.consent}
      </h2>
      {detail.consent ? (
        <>
          <p className="m-0 text-15 leading-6">{T.consentWith(detail.consent.recipientName)}</p>
          <span className="text-13 text-muted">
            {c.wizard.docs.recordedOn} <bdi dir="ltr">{formatDate(detail.consent.recordedAt, { numerals })}</bdi>
          </span>
          {!terminal ? (
            confirming ? (
              <div className="flex flex-col gap-2 rounded-md border border-warn-line bg-warn-bg p-3">
                <p className="m-0 text-15 leading-6">{T.withdrawConsentConfirm}</p>
                {withdraw.error ? <Alert tone="err">{withdraw.error}</Alert> : null}
                <div className="flex flex-wrap gap-2">
                  <Button variant="sensitive" size="lg" loading={withdraw.busy} onClick={() => void doWithdraw()}>
                    {T.withdrawConsent}
                  </Button>
                  <Button variant="secondary" size="lg" onClick={() => setConfirming(false)}>
                    {T.keepConsent}
                  </Button>
                </div>
              </div>
            ) : (
              <Button variant="text" size="lg" className="self-start text-15" onClick={() => setConfirming(true)}>
                {T.withdrawConsent}
              </Button>
            )
          ) : null}
        </>
      ) : (
        <>
          <p className="m-0 text-15 leading-6">{T.consentNone}</p>
          {!terminal ? (
            <Button href={`/my/requests/${ref}/consent`} variant="secondary" size="lg" className="self-start">
              {T.renewConsent}
            </Button>
          ) : null}
        </>
      )}
    </Panel>
  );
}
