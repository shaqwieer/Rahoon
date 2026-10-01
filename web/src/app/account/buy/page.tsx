import Link from "next/link";
import { BuyerAccount } from "@/components/market/BuyerAccount";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { Card } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import { getCatalog } from "@/lib/market/server";
import type { BuyerRequestView, Catalog, Fit, MarketEvent, OpportunityCard as CardT } from "@/lib/market/types";

interface Mine {
  request: BuyerRequestView | null;
  events?: MarketEvent[];
  suggestions: { card: CardT; fit: Fit }[];
}

/** The buyer request, its review, and suggested opportunities with why each fits and its limits. */
export default async function MyBuyerRequestPage() {
  const [mine, catalog] = await Promise.all([apiGet<Mine>("/market/buyer-requests/mine"), getCatalog() as Promise<Catalog>]);
  if (!mine.request)
    return (
      <Card title="طلب الشراء">
        <p className="m-0 mb-4 text-15 text-muted">لم تسجل قدرتك الشرائية بعد. سجّلها ليقترح عليك الفريق الفرص المناسبة.</p>
        <Link href="/buy/new" className={buttonClasses({ variant: "primary", size: "lg" })}>سجّل قدرتك الشرائية</Link>
      </Card>
    );
  const live = !["withdrawn", "rejected"].includes(mine.request.status);
  return (
    <div className="flex flex-col gap-6">
      <BuyerAccount request={mine.request} events={mine.events ?? []} catalog={catalog} />
      {!live ? (
        <Link href="/buy/new" className={buttonClasses({ variant: "primary", size: "lg", className: "self-start" })}>بدء طلب شراء جديد</Link>
      ) : (
        <section aria-labelledby="sugg-h" className="flex flex-col gap-4">
          <h2 id="sugg-h" className="m-0 text-22 font-bold">فرص منشورة تطابق مدنك وأنواعك</h2>
          {mine.suggestions.length === 0 ? (
            <p className="m-0 text-15 text-muted">لا توجد فرص منشورة قابلة للمقارنة بتفضيلاتك الآن. سيقترح الفريق عليك ما يناسب عند توفره.</p>
          ) : (
            <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
              {mine.suggestions.map((s) => (
                <li key={s.card.reference}><OpportunityCard card={s.card} fit={s.fit} signedIn /></li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}
