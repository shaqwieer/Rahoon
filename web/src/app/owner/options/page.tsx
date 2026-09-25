import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerOptions } from "@/components/owner/types";
import { InfoNote } from "@/components/owner/ui";
import { ServerText } from "@/components/owner/values";
import { Button } from "@/components/ui";
import { InquiryButton } from "./InquiryButton";

export const generateMetadata = () => ownerMetadata((c) => c.options.title);

/** D06 — the offer sent to the owner (if any) and other options they can ask about (voluntary sale: owner-initiated only). */
export default async function OwnerOptionsPage() {
  const { c, shell } = await getOwnerContext();
  const o = await ownerGet<OwnerOptions>("/options");
  const O = c.options;
  return (
    <OwnerPage shell={shell} title={O.title} active="options">
      {o.activeOffer ? (
        <article aria-labelledby="offer-title" className="flex flex-col gap-2 rounded-[12px] border-2 border-ink bg-white p-4">
          <ServerText text={o.activeOffer.eyebrow} className="self-start rounded-pill bg-rust-50 px-2.5 py-0.5 text-13 font-bold text-rust-700" />
          <h2 id="offer-title" className="m-0 text-19 font-bold">
            <ServerText text={o.activeOffer.title} />
          </h2>
          <ServerText as="p" text={o.activeOffer.body} className="m-0 text-16 leading-[26px]" />
          <Button href={`/owner/offers/${o.activeOffer.id}`} size="lg" className="min-h-[50px] rounded-[8px]">
            {O.details}
          </Button>
        </article>
      ) : (
        <InfoNote tone="neutral" icon="tips_and_updates">
          {O.noOffer}
        </InfoNote>
      )}
      <h2 className="m-0 mt-1 text-16 font-bold">{O.others}</h2>
      <p id="inquiry-note" className="-mt-2 m-0 text-14 text-muted">
        {O.inquiryNote}
      </p>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {o.otherOptions.map((x) => (
          <li key={x.key} className="flex flex-col gap-1 rounded-[12px] border border-line bg-white px-4 pt-3.5 pb-1.5">
            <ServerText as="strong" text={x.title} className="text-16" />
            <ServerText text={x.description} className="text-15 leading-6 text-charcoal" />
            <InquiryButton optionKey={x.key} label={x.link} describedBy="inquiry-note" />
          </li>
        ))}
      </ul>
    </OwnerPage>
  );
}
