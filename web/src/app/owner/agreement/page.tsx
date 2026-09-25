import type { ReactNode } from "react";
import { OwnerHeading, OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerAgreement } from "@/components/owner/types";
import { Amount, ServerText } from "@/components/owner/values";
import { DateText, EmptyState, Tag } from "@/components/ui";
import { daysText } from "@/lib/format";
import { PrintButton } from "./PrintButton";

export const generateMetadata = () => ownerMetadata((c) => c.agreement.title);

/** Agreement view — printable terms (print CSS hides the portal chrome) with the consent record note. */
export default async function OwnerAgreementPage() {
  const { c, shell, fmt } = await getOwnerContext();
  const { agreement: a } = await ownerGet<OwnerAgreement>("/agreement");
  const A = c.agreement;

  if (!a) {
    return (
      <OwnerPage shell={shell} title={A.title} backHref="/owner" backLabel={c.backHome} active="home">
        <EmptyState icon="description" title={A.emptyTitle} body={A.emptyBody} />
      </OwnerPage>
    );
  }

  const missed = a.breachMissedConsecutive === 1 ? c.offer.missedOne : a.breachMissedConsecutive === 2 ? c.offer.missedTwo : c.offer.missedN(a.breachMissedConsecutive);
  const rows: Array<{ k: string; v: ReactNode }> = [
    { k: A.lender, v: <ServerText text={a.lender} /> },
    { k: A.version, v: <bdi dir="ltr">{a.versionLabel}</bdi> },
    { k: A.rescheduled, v: <Amount value={a.rescheduledAmount} /> },
    ...(a.waiverAmount > 0 ? [{ k: A.waiver, v: <Amount value={a.waiverAmount} /> }] : []),
    { k: A.installments, v: <bdi dir="ltr">{a.installmentCount}</bdi> },
    { k: A.installment, v: <Amount value={a.installmentAmount} /> },
    { k: A.dueDay, v: A.dueDayValue(a.dueDay) },
    { k: A.start, v: <DateText value={a.startDate} mode="both" /> },
    { k: A.end, v: <DateText value={a.endDate} /> },
  ];

  return (
    <OwnerPage shell={shell} title={A.title} backHref="/owner" backLabel={c.backHome} active="home" heading="none">
      <article aria-labelledby="agreement-h" className="flex flex-col gap-3.5 rounded-[12px] border border-line bg-white p-5 print:border-0 print:p-0">
        <div className="flex flex-col gap-1.5">
          <div id="agreement-h">
            <OwnerHeading title={A.heading} visible />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <bdi dir="ltr" className="font-mono text-16 font-semibold">
              {a.number}
            </bdi>
            <Tag tone={a.status === "Active" || a.status === "Completed" ? "ok" : a.status === "PendingActivation" ? "info" : "warn"}>{A.status[a.status] ?? a.status}</Tag>
          </div>
        </div>
        <dl className="m-0 flex flex-col">
          {rows.map((r, i) => (
            <div key={i} className="flex items-baseline justify-between gap-3 border-t border-divider py-2.5 text-16 first:border-t-0">
              <dt className="text-charcoal">{r.k}</dt>
              <dd className="m-0 text-end font-semibold">{r.v}</dd>
            </div>
          ))}
        </dl>
        <section aria-labelledby="breach-h" className="flex flex-col gap-1">
          <h2 id="breach-h" className="m-0 text-16 font-bold">
            {A.breach}
          </h2>
          <p className="m-0 text-16 leading-[26px]">{c.offer.lateBody(missed, daysText(a.breachCureDays, fmt))}</p>
        </section>
        <section aria-labelledby="consent-h" className="flex flex-col gap-1 rounded-[8px] bg-subtle p-3 print:bg-transparent print:p-0">
          <h2 id="consent-h" className="m-0 text-15 font-bold">
            {A.consent}
          </h2>
          {a.consentAt ? (
            <span className="text-15">
              {A.consentAt}: <DateText value={a.consentAt} mode="datetime" /> · <DateText value={a.consentAt} mode="hijri" />
            </span>
          ) : null}
          <ServerText as="p" text={a.signature} className="m-0 text-14 leading-[22px] text-muted" />
        </section>
      </article>
      <PrintButton label={A.print} hint={A.printHint} />
    </OwnerPage>
  );
}
