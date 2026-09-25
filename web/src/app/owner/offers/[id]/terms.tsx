import type { ReactNode } from "react";
import type { OwnerCopy } from "@/components/owner/copy";
import type { OwnerOffer } from "@/components/owner/types";
import { Amount } from "@/components/owner/values";
import { DateText } from "@/components/ui";
import { daysText, type FormatLocale, type Numerals } from "@/lib/format";

/** Hero (installment or one-off amount) shared by D07 and the D09 full-terms dialog. */
export function OfferHero({ offer, c }: { offer: OwnerOffer; c: OwnerCopy }) {
  const payoff = offer.kind === "ReducedPayoff";
  return (
    <div className="flex flex-col gap-0.5 rounded-[12px] border border-line bg-white p-4">
      <span className="text-16">{payoff ? c.offer.heroPayoff : c.offer.heroInstallment}</span>
      <Amount value={payoff ? offer.rescheduled : offer.installment} className="text-[34px] leading-[46px] font-bold" unitClassName="text-16 font-medium" />
      <span className="text-16">{payoff ? c.offer.heroPayoffSub : c.offer.heroSub(offer.dueDay, offer.termMonths)}</span>
    </div>
  );
}

/** Key terms (dl) — start, end, waived late fees, «بيتك يبقى ملكك» (never for a voluntary-sale solution). */
export function OfferTerms({ offer, c }: { offer: OwnerOffer; c: OwnerCopy }) {
  const O = c.offer;
  const rows: Array<{ k: string; v: ReactNode }> = [
    { k: O.starts, v: <DateText value={offer.start} /> },
    { k: O.ends, v: <DateText value={offer.end} /> },
  ];
  if (offer.waiver > 0) {
    rows.push({
      k: O.lateFees,
      v: (
        <span>
          {O.waived} (<Amount value={offer.waiver} fractionDigits={0} />)
        </span>
      ),
    });
  }
  if (offer.kind !== "VoluntarySale") rows.push({ k: O.home, v: O.homeStays });
  return (
    <dl className="m-0 flex flex-col overflow-hidden rounded-[12px] border border-line bg-white">
      {rows.map((r, i) => (
        <div key={i} className="flex items-baseline justify-between gap-3 border-t border-divider px-3.5 py-3 text-16 first:border-t-0">
          <dt className="text-charcoal">{r.k}</dt>
          <dd className="m-0 text-end font-bold">{r.v}</dd>
        </div>
      ))}
    </dl>
  );
}

/** «ماذا لو تأخرت عن قسط؟» body from the offer's breach terms (missed installments + cure days). */
export function lateText(offer: OwnerOffer, c: OwnerCopy, fmt: { locale: FormatLocale; numerals: Numerals }): string {
  const O = c.offer;
  const n = offer.missedConsecutive;
  const missed = n === 1 ? O.missedOne : n === 2 ? O.missedTwo : O.missedN(n);
  return O.lateBody(missed, daysText(offer.cureDays, fmt));
}
