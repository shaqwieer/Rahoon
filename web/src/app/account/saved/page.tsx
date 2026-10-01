import Link from "next/link";
import { CompareTray } from "@/components/market/discovery/CompareControls";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { Badge, Card } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import type { OpportunityCard as CardT } from "@/lib/market/types";

type Saved = { reference: string; available: true; card: CardT } | { reference: string; available: false; title: string; card: null; statusLabel: string };

/** Saved opportunities. One no longer published shows only that it is unavailable — none of the figures it used to show. */
export default async function SavedPage() {
  const rows = await apiGet<Saved[]>("/market/my/saved");
  return (
    <div className="flex flex-col gap-4 pb-16">
      <h1 className="m-0 text-24 font-bold">الفرص المحفوظة</h1>
      {rows.length === 0 ? (
        <Card><p className="m-0 text-15 text-muted">لا توجد فرص محفوظة. احفظ ما يعجبك من <Link href="/opportunities">الفرص المتاحة</Link>.</p></Card>
      ) : (
        <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
          {rows.map((r) => (
            <li key={r.reference} className="flex flex-col gap-2">
              {r.available ? (
                <OpportunityCard card={r.card} signedIn />
              ) : (
                <div className="flex h-full flex-col gap-2 rounded-lg border border-dashed border-line-strong bg-white p-4">
                  <Badge tone="warn">{r.statusLabel}</Badge>
                  <strong className="text-15">{r.title}</strong>
                  <bdi dir="ltr" className="font-mono text-12 text-muted">{r.reference}</bdi>
                  <span className="text-13 text-muted">أُوقف عرض هذه الفرصة أو سُحبت؛ لا نعرض أرقامها بعد ذلك. ستعود هنا إن نُشرت من جديد.</span>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      <CompareTray />
    </div>
  );
}
