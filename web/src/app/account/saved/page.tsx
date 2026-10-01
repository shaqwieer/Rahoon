import Link from "next/link";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { Badge, Card } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import type { OpportunityCard as CardT } from "@/lib/market/types";

export default async function SavedPage() {
  const rows = await apiGet<{ card: CardT; available: boolean; statusLabel: string }[]>("/market/my/saved");
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 font-bold">الفرص المحفوظة</h1>
      {rows.length === 0 ? (
        <Card><p className="m-0 text-15 text-muted">لا توجد فرص محفوظة. احفظ ما يعجبك من <Link href="/opportunities">الفرص المتاحة</Link>.</p></Card>
      ) : (
        <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
          {rows.map((r) => (
            <li key={r.card.reference} className="flex flex-col gap-2">
              {!r.available ? <Badge tone="warn">لم تعد متاحة: {r.statusLabel}</Badge> : null}
              <OpportunityCard card={r.card} signedIn />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
