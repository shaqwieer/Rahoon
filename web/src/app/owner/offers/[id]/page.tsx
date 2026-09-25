import type { ReactNode } from "react";
import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerOffer } from "@/components/owner/types";
import { Card, InfoNote, TouchLink } from "@/components/owner/ui";
import { Button, DateText, Icon } from "@/components/ui";
import { DeclineButton, RequestNewOffer } from "./OfferActions";
import { lateText, OfferHero, OfferTerms } from "./terms";

export const generateMetadata = () => ownerMetadata((c) => c.offer.title);

/** D07 — the offer: hero, key terms, what happens if late; accept / suggest a change / decline, or request a new one when expired. */
export default async function OwnerOfferPage({ params }: PageProps<"/owner/offers/[id]">) {
  const { id } = await params;
  const { c, shell, fmt } = await getOwnerContext();
  const offer = await ownerGet<OwnerOffer>(`/offers/${encodeURIComponent(id)}`);
  const O = c.offer;
  const open = offer.status === "Sent";
  const sub: ReactNode = open ? (
    <>
      {O.validUntil} <DateText value={offer.validUntil} />
    </>
  ) : undefined;

  return (
    <OwnerPage shell={shell} title={O.title} sub={sub} backHref="/owner/options" backLabel={c.back} hideNav active="options">
      <OfferHero offer={offer} c={c} />
      <OfferTerms offer={offer} c={c} />
      <details open className="group rounded-[12px] border border-line bg-white px-3.5 py-3">
        <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-2 text-16 font-semibold [&::-webkit-details-marker]:hidden">
          {O.whatIfLate}
          <Icon name="expand_more" size={22} className="transition-transform group-open:rotate-180" />
        </summary>
        <p className="m-0 mt-2 text-16 leading-[26px]">{lateText(offer, c, fmt)}</p>
      </details>

      {open ? (
        <div className="mt-1 flex flex-col gap-2.5">
          <Button href={`/owner/offers/${offer.id}/accept`} size="xl" fullWidth className="text-17">
            {O.reviewAccept}
          </Button>
          <div className="grid grid-cols-2 gap-2.5">
            <Button href={`/owner/offers/${offer.id}/counter`} variant="secondary" size="lg" className="min-h-[50px] rounded-[8px] text-15">
              {O.counter}
            </Button>
            <DeclineButton offerId={offer.id} />
          </div>
        </div>
      ) : offer.status === "Expired" ? (
        <Card className="gap-3 p-4" as="section" aria-labelledby="expired-title">
          <h2 id="expired-title" className="m-0 flex items-center gap-2 text-18 font-bold">
            <Icon name="event_busy" size={24} className="text-warn" />
            {O.expiredTitle}
          </h2>
          <p className="m-0 text-16 leading-[26px]">{O.expiredBody}</p>
          <RequestNewOffer version={offer.version} />
        </Card>
      ) : (
        <div role="status" className="flex flex-col gap-1">
          <InfoNote icon={offer.status === "Accepted" ? "task_alt" : "info"}>
            {offer.status === "Accepted" ? O.accepted : offer.status === "Countered" ? O.countered : offer.status === "Declined" ? O.declined : O.withdrawn}
          </InfoNote>
          {offer.status === "Accepted" ? <TouchLink href="/owner/agreement">{O.viewAgreement}</TouchLink> : <TouchLink href="/owner/messages">{O.messages}</TouchLink>}
        </div>
      )}
    </OwnerPage>
  );
}
