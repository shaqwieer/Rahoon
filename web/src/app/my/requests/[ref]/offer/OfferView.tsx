"use client";

import type { ReactNode } from "react";
import { IndividualFrame, Panel, useRequestCopy } from "@/components/individual/ui";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { Tag } from "@/components/ui/Status";
import type { MyOffer, MyRequestDetail } from "@/lib/api/requests";
import { formatDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

/** The typed terms per path (P1/P2/P3) as key–value rows; shared with the acceptance screen. */
export function OfferTerms({ offer }: { offer: MyOffer }) {
  const c = useRequestCopy();
  const O = c.offer;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const rows: Array<{ key: string; value: ReactNode }> = [];
  if (offer.path === "p1") {
    if (offer.newInstallment !== null) rows.push({ key: O.newInstallment, value: <strong className="text-18">{formatMoney(offer.newInstallment, fmt)}</strong> });
    if (offer.termMonths !== null) rows.push({ key: O.termMonths, value: O.months(offer.termMonths) });
    if (offer.startText) rows.push({ key: O.start, value: offer.startText });
  }
  if (offer.path === "p2") {
    if (offer.settlementAmount !== null) rows.push({ key: O.settlementAmount, value: <strong className="text-18">{formatMoney(offer.settlementAmount, fmt)}</strong> });
    if (offer.paymentConditions) rows.push({ key: O.paymentConditions, value: offer.paymentConditions });
    if (offer.remainingText) rows.push({ key: O.remaining, value: offer.remainingText });
  }
  if (offer.path === "p3" && offer.saleTerms) rows.push({ key: O.saleTerms, value: <span className="whitespace-pre-line">{offer.saleTerms}</span> });
  if (offer.conditions) rows.push({ key: O.conditions, value: offer.conditions });
  rows.push({ key: O.lenderRef, value: <bdi dir="ltr" className="font-mono">{offer.lenderReference}</bdi> });
  if (offer.lenderValidityText)
    rows.push({
      key: O.validityLabel,
      value: (
        <span>
          {offer.lenderValidityText} <span className="text-13 text-muted">({O.validity})</span>
        </span>
      ),
    });
  return <KeyValueList rows={rows} />;
}

export function OfferView({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const O = c.offer;
  const { numerals } = useI18n();
  const ref = encodeURIComponent(detail.reference);
  const offer = detail.offer;

  return (
    <IndividualFrame title={O.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}` }}>
      {!offer ? (
        <p className="m-0 text-17">{O.none}</p>
      ) : (
        <>
          <div className="flex flex-col gap-2">
            <span className="text-14 font-bold text-rust-700">{O.eyebrow}</span>
            <h1 className="m-0 text-24 leading-9 font-bold">{O.pathLabel[offer.path]}</h1>
            <p className="m-0 flex items-start gap-1.5 text-15 leading-6 text-charcoal">
              <Icon name="verified" size={20} className="text-ok" />
              <span>{O.note(detail.institutionName, formatDate(offer.lenderLetterDate, { numerals }))}</span>
            </p>
            {offer.letterVersionId ? (
              <a href={`/api/my/requests/${ref}/documents/${offer.letterVersionId}/file`} className="inline-flex min-h-11 items-center gap-1.5 self-start text-15 font-semibold">
                <Icon name="description" size={20} />
                {O.letter}
              </a>
            ) : null}
          </div>

          <Panel aria-labelledby="terms-h">
            <h2 id="terms-h" className="m-0 text-18 font-bold">
              {O.terms}
            </h2>
            <OfferTerms offer={offer} />
          </Panel>

          <Panel aria-labelledby="effect-h" className="border-t-4 border-t-orange">
            <h2 id="effect-h" className="m-0 text-18 font-bold">
              {O.effect}
            </h2>
            <p className="m-0 text-17 leading-7 whitespace-pre-line">{offer.effectText}</p>
          </Panel>

          <p className="m-0 flex items-center gap-1.5 text-15 font-semibold">
            <Icon name="info" size={20} />
            {O.notFinal}
          </p>

          {detail.canRespond ? (
            <div className="flex flex-col gap-2.5">
              <Button href={`/my/requests/${ref}/offer/accept`} size="xl" fullWidth>
                {O.accept}
              </Button>
              <Button href={`/my/requests/${ref}/offer/respond?kind=question`} variant="secondary" size="xl" fullWidth>
                {O.question}
              </Button>
              <Button href={`/my/requests/${ref}/offer/respond?kind=decline`} variant="text" size="lg" className="self-center text-16">
                {O.decline}
              </Button>
            </div>
          ) : detail.responses.length > 0 ? (
            <Tag tone="info" icon="task_alt" className="self-start">
              {O.responseKinds[detail.responses[0].kind]}
            </Tag>
          ) : null}
          <p className="m-0 text-13 leading-5 text-muted">{c.paths.footnote}</p>
        </>
      )}
    </IndividualFrame>
  );
}
