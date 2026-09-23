import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata, riyadhToday } from "@/components/owner/server";
import type { OwnerOffer } from "@/components/owner/types";
import { InfoNote, TouchLink } from "@/components/owner/ui";
import { CounterForm } from "./CounterForm";

export const generateMetadata = () => ownerMetadata((c) => c.counter.title);

/** Next six months (yyyy-MM) from the Riyadh calendar, labelled «ديسمبر 2026» / "December 2026". */
function nextMonths(locale: "ar" | "en") {
  const [y, m] = riyadhToday().split("-").map(Number);
  const fmt = new Intl.DateTimeFormat(locale === "en" ? "en-US" : "ar-SA-u-ca-gregory-nu-latn", { month: "long", year: "numeric", timeZone: "UTC" });
  return Array.from({ length: 6 }, (_, i) => {
    const d = new Date(Date.UTC(y, m - 1 + i + 1, 1));
    const value = `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}`;
    return { value, label: fmt.format(d).replace(/[؜‎‏]/g, "") };
  });
}

/** D08 — suggest a change to an open offer (day of month, first month, amount) with an optional reason. */
export default async function OwnerCounterPage({ params }: PageProps<"/owner/offers/[id]/counter">) {
  const { id } = await params;
  const { c, shell, fmt } = await getOwnerContext();
  const offer = await ownerGet<OwnerOffer>(`/offers/${encodeURIComponent(id)}`);
  const offerHref = `/owner/offers/${offer.id}`;
  return (
    <OwnerPage shell={shell} title={c.counter.title} backHref={offerHref} backLabel={c.consent.backToOffer} hideNav active="options">
      {offer.status === "Sent" ? (
        <>
          <p className="m-0 text-17 leading-7">{c.counter.intro}</p>
          <CounterForm offerId={offer.id} months={nextMonths(fmt.locale)} />
        </>
      ) : (
        <div role="status" className="flex flex-col gap-1">
          <InfoNote>{offer.status === "Countered" ? c.offer.countered : offer.status === "Expired" ? c.offer.expiredTitle : c.consent.notOpen}</InfoNote>
          <TouchLink href={offerHref}>{c.consent.backToOffer}</TouchLink>
        </div>
      )}
    </OwnerPage>
  );
}
