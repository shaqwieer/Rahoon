import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerOffer } from "@/components/owner/types";
import { InfoNote, TouchLink } from "@/components/owner/ui";
import { ConsentFlow } from "./ConsentFlow";

export const generateMetadata = () => ownerMetadata((c) => c.consent.title);

/**
 * D09 — consent & acceptance. Only an open offer shows the form; after a reload an accepted offer shows the
 * "already recorded" state with the agreement link (the server is the authority).
 */
export default async function OwnerAcceptPage({ params }: PageProps<"/owner/offers/[id]/accept">) {
  const { id } = await params;
  const { c, shell } = await getOwnerContext();
  const offer = await ownerGet<OwnerOffer>(`/offers/${encodeURIComponent(id)}`);
  if (offer.status === "Sent") return <ConsentFlow offer={offer} shell={shell} />;

  const offerHref = `/owner/offers/${offer.id}`;
  const accepted = offer.status === "Accepted";
  return (
    <OwnerPage shell={shell} title={accepted ? c.consent.doneTitle : c.consent.title} backHref={offerHref} backLabel={c.consent.backToOffer} hideNav active="options">
      <div role="status" className="flex flex-col gap-1">
        <InfoNote icon={accepted ? "task_alt" : "info"}>{accepted ? c.consent.alreadyAccepted : offer.status === "Expired" ? c.offer.expiredTitle : c.consent.notOpen}</InfoNote>
        {accepted ? <TouchLink href="/owner/agreement">{c.offer.viewAgreement}</TouchLink> : <TouchLink href={offerHref}>{c.consent.backToOffer}</TouchLink>}
      </div>
    </OwnerPage>
  );
}
