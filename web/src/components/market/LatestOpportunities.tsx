import Link from "next/link";
import { M } from "@/components/market/copy";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { apiFetch } from "@/lib/api/server";
import { parseResponse } from "@/lib/api/types";
import { optionalIndividual } from "@/lib/market/server";
import type { SearchResult } from "@/lib/market/types";

/** The three latest published opportunities (real data only; an honest empty state otherwise). */
export async function LatestOpportunities() {
  const [res, me] = await Promise.all([
    apiFetch("/market/opportunities?pageSize=3&sort=newest").then((r) => parseResponse<SearchResult>(r)).catch(() => null),
    optionalIndividual(),
  ]);
  if (res && res.items.length > 0)
    return (
      <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
        {res.items.map((i) => (
          <li key={i.card.reference}>
            <OpportunityCard card={i.card} signedIn={Boolean(me)} />
          </li>
        ))}
      </ul>
    );
  return (
    <div className="flex flex-col items-start gap-4 rounded-lg border border-dashed border-line-strong bg-white p-6 md:flex-row md:items-center md:justify-between">
      <p className="m-0 flex gap-2 text-15 leading-7 text-charcoal">
        <Icon name="home_work" size={22} className="mt-0.5 text-muted" />
        {res ? M.home.latestEmpty : "تعذّر تحميل الفرص الآن. حاول لاحقًا."}
      </p>
      <Link href="/buy/new" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 flex-none" })}>
        {M.home.latestEmptyCta}
      </Link>
    </div>
  );
}
