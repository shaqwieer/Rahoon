import Link from "next/link";
import { BuyerAccount } from "@/components/market/BuyerAccount";
import { CompareTray } from "@/components/market/discovery/CompareControls";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { Card } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import { getCatalog } from "@/lib/market/server";
import type { BuyerRequestView, Catalog, MarketEvent, SearchItem } from "@/lib/market/types";

interface Mine {
  request: BuyerRequestView | null;
  events?: MarketEvent[];
  suggestions: SearchItem[];
  incomplete: SearchItem[];
  matching: { revision: number; eligible: number; fits: number; incomplete: number; doesNotFit: number; searchUrl: string; sortExplanation: string } | null;
}

/** The buyer request, its review, and explained matches: what fits, what can't be compared yet, and why. */
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
  const m = mine.matching;
  return (
    <div className="flex flex-col gap-6 pb-16">
      <BuyerAccount request={mine.request} events={mine.events ?? []} catalog={catalog} />
      {!live ? (
        <Link href="/buy/new" className={buttonClasses({ variant: "primary", size: "lg", className: "self-start" })}>بدء طلب شراء جديد</Link>
      ) : m ? (
        <section aria-labelledby="sugg-h" className="flex flex-col gap-4">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div className="flex flex-col gap-1">
              <h2 id="sugg-h" className="m-0 text-22 font-bold">فرص تطابق طلبك</h2>
              <span className="text-14 text-charcoal">
                من {m.eligible} {m.eligible === 1 ? "فرصة منشورة" : "فرص منشورة"} في مدنك وأنواعك: <strong>{m.fits}</strong> تناسب أرقامك، و{m.incomplete} أرقامها غير مكتملة للمقارنة، و{m.doesNotFit} لا تناسب.
              </span>
              <span className="text-13 text-muted">محسوبة من تحديث طلبك رقم {m.revision}. الترتيب: {m.sortExplanation}</span>
            </div>
            <Link href={m.searchUrl} className={buttonClasses({ variant: "secondary", size: "md" })}>عرضها في البحث والخريطة</Link>
          </div>
          {mine.suggestions.length === 0 ? (
            <p className="m-0 text-15 text-muted">لا توجد فرص منشورة تناسب أرقامك الآن. احفظ بحثًا بقدرتك من صفحة البحث لتصلك الفرص الجديدة.</p>
          ) : (
            <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
              {mine.suggestions.map((s) => <li key={s.card.reference}><OpportunityCard card={s.card} fit={s.fit} match={s.match} signedIn /></li>)}
            </ul>
          )}
          {mine.incomplete.length ? (
            <>
              <h3 className="m-0 mt-2 text-18 font-bold">لا يمكن مقارنتها بعد</h3>
              <p className="m-0 text-14 text-muted">بعض أرقام هذه الفرص غير معروفة (مثل المطلوب الآن أو القسط)، فلا نقول إنها تناسبك ولا إنها لا تناسبك.</p>
              <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
                {mine.incomplete.map((s) => <li key={s.card.reference}><OpportunityCard card={s.card} fit={s.fit} match={s.match} signedIn /></li>)}
              </ul>
            </>
          ) : null}
          <p className="m-0 text-13 text-muted">المطابقة ترتيب يساعدك، وليست موافقة تمويل ولا ضمانًا بأن الفرصة في متناولك. قسط صاحب العقار في العقار المموّل لا ينتقل إليك.</p>
        </section>
      ) : (
        <p className="m-0 text-15 text-muted">نقترح عليك الفرص بعد إرسال طلب الشراء.</p>
      )}
      <CompareTray />
    </div>
  );
}
