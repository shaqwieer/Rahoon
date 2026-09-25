import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerJourney } from "@/components/owner/types";
import { InfoNote } from "@/components/owner/ui";
import { ServerText } from "@/components/owner/values";
import { Icon } from "@/components/ui";
import { cn } from "@/lib/cn";

export const generateMetadata = () => ownerMetadata((c) => c.journey.title);

/** D03 — the five plain-language stages (referral is never shown as an expected stage). */
export default async function OwnerJourneyPage() {
  const { c, shell, fmt } = await getOwnerContext();
  const j = await ownerGet<OwnerJourney>("/journey");
  const en = fmt.locale === "en";
  return (
    <OwnerPage shell={shell} title={c.journey.title} backHref="/owner" backLabel={c.backHome} active="home">
      <ol className="m-0 flex list-none flex-col p-0">
        {j.steps.map((s, i) => {
          const last = i === j.steps.length - 1;
          return (
            <li key={i} aria-current={s.status === "current" ? "step" : undefined} className="grid grid-cols-[32px_minmax(0,1fr)] gap-3.5">
              <div className="flex flex-col items-center">
                <span
                  className={cn(
                    "box-border flex size-8 flex-none items-center justify-center rounded-full border-2",
                    s.status === "done" ? "border-charcoal bg-charcoal text-white" : s.status === "current" ? "border-orange bg-rust-50 text-rust-700" : "border-line bg-white",
                  )}
                >
                  {s.status === "todo" ? null : <Icon name={s.status === "done" ? "check" : "more_horiz"} size={18} />}
                </span>
                {last ? null : <span aria-hidden="true" className="min-h-5 w-0.5 flex-1 bg-line" />}
              </div>
              <div className={cn("flex flex-col gap-1", last ? "pb-2" : "pb-[18px]")}>
                <strong className="text-17 leading-[26px]">{en ? (c.stages[i] ?? s.title) : s.title}</strong>
                <ServerText text={s.description} className="text-15 leading-6 text-charcoal" />
                <span className="text-13 text-muted">
                  {s.status === "current" ? c.journey.now : s.meta === "—" ? <span aria-hidden="true">—</span> : <bdi dir="ltr">{s.meta}</bdi>}
                </span>
              </div>
            </li>
          );
        })}
      </ol>
      <InfoNote>
        <ServerText text={j.note} />
      </InfoNote>
    </OwnerPage>
  );
}
