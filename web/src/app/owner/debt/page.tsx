import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerDebt } from "@/components/owner/types";
import { Card, TouchLink } from "@/components/owner/ui";
import { Amount, ServerText } from "@/components/owner/values";
import { EmptyState } from "@/components/ui";

export const generateMetadata = () => ownerMetadata((c) => c.debt.title);

/** D05 — what you owe now: lender figures with the update date, each line explained in plain language. */
export default async function OwnerDebtPage() {
  const { c, shell, fmt } = await getOwnerContext();
  const d = await ownerGet<OwnerDebt>("/debt");
  const D = c.debt;
  const en = fmt.locale === "en";
  return (
    <OwnerPage shell={shell} title={D.title} backHref="/owner" backLabel={c.backHome} active="home">
      {d.total === null || d.total === undefined ? (
        <EmptyState icon="receipt_long" title={D.emptyTitle} body={D.emptyBody} />
      ) : (
        <>
          <Card className="gap-1 p-4">
            <span className="text-16 text-charcoal">{D.total}</span>
            <Amount value={d.total} className="text-32 leading-[44px] font-bold" unitClassName="text-16 font-medium" />
            <ServerText text={d.source} className="text-13 text-muted" />
          </Card>
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {(d.items ?? []).map((i) => (
              <li key={i.key} className="flex flex-col gap-1 rounded-[12px] border border-line bg-white px-4 py-3.5">
                <div className="flex items-baseline justify-between gap-2">
                  <strong className="text-16">{en ? (D.labels[i.key] ?? i.label) : <ServerText text={i.label} />}</strong>
                  <Amount value={i.amount} hideUnit className="text-16 font-bold" />
                </div>
                <ServerText text={i.explanation} className="text-15 leading-6 text-charcoal" />
              </li>
            ))}
          </ul>
          <TouchLink href="/owner/complaints/new?type=objection">{D.report}</TouchLink>
        </>
      )}
    </OwnerPage>
  );
}
