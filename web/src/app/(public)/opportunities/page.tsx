import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { OpportunityCard } from "@/components/market/OpportunityCard";
import { SearchFilters } from "@/components/market/SearchFilters";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { apiFetch } from "@/lib/api/server";
import { parseResponse } from "@/lib/api/types";
import { sar } from "@/lib/market/format";
import { getCatalog, optionalIndividual } from "@/lib/market/server";
import type { SearchResult } from "@/lib/market/types";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = {
  title: "الفرص المتاحة",
  description: "فرص عقارية في السعودية تعرض المبلغ المطلوب منك الآن منفصلًا عن سعر الشراء والرصيد والأقساط، مع توضيح ما تم التحقق منه.",
};

const PASS = ["city", "types", "maxNow", "maxInstallment", "freq", "maxTotal", "readiness", "minArea", "bedrooms", "track", "sort", "page"];

/** Published opportunities with server-side filtering and pagination; the URL holds the search so it can be shared. */
export default async function OpportunitiesPage({ searchParams }: PageProps<"/opportunities">) {
  const sp = await searchParams;
  const q = new URLSearchParams();
  for (const k of PASS) {
    const v = sp[k];
    if (typeof v === "string" && v) q.set(k, v);
  }
  q.set("pageSize", "12");
  const [catalog, me] = await Promise.all([getCatalog(), optionalIndividual()]);
  let result: SearchResult | null = null;
  let failed = false;
  try {
    result = await parseResponse<SearchResult>(await apiFetch(`/market/opportunities?${q}`));
  } catch {
    failed = true;
  }
  const page = result?.page ?? 1;
  const link = (p: number) => {
    const x = new URLSearchParams(q);
    x.delete("pageSize");
    if (p > 1) x.set("page", String(p));
    else x.delete("page");
    return x.toString() ? `/opportunities?${x}` : "/opportunities";
  };
  const without = (keys: string[], extra?: Record<string, string>) => {
    const x = new URLSearchParams(q);
    x.delete("pageSize");
    x.delete("page");
    keys.forEach((k) => x.delete(k));
    for (const [k, v] of Object.entries(extra ?? {})) x.set(k, v);
    return x.toString() ? `/opportunities?${x}` : "/opportunities";
  };
  const maxNow = Number(q.get("maxNow") ?? 0);

  return (
    <PublicPage wide title="الفرص المتاحة" lead="اعثر على فرصة تناسب ميزانيتك بأرقام واضحة: المطلوب منك الآن، ثم ما تلتزم به لاحقًا.">
      <div className="flex flex-col gap-6">
        <Suspense fallback={<div className="h-48 animate-rh-pulse rounded-lg bg-subtle" />}>
          <SearchFilters catalog={catalog} total={result?.total ?? 0} excluded={result?.excludedIncomplete ?? 0} />
        </Suspense>
        {failed ? (
          <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-err-line bg-err-bg p-5">
            <strong>تعذّر تحميل الفرص الآن.</strong>
            <span className="text-14">تحقق من اتصالك ثم أعد المحاولة.</span>
            <Link href={link(page)} className={buttonClasses({ variant: "secondary", size: "md" })}>إعادة المحاولة</Link>
          </div>
        ) : result && result.items.length === 0 ? (
          <div className="flex flex-col gap-4 rounded-lg border border-dashed border-line-strong bg-white p-6">
            <span className="flex items-center gap-2 text-17 font-bold">
              <Icon name="search_off" size={24} className="text-muted" />
              لا توجد فرص منشورة تطابق اختياراتك الآن
            </span>
            {q.size > 1 ? (
              <>
                <span className="text-14 text-charcoal">يمكنك تعديل البحث بنفسك — لن نوسّعه دون علمك:</span>
                <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
                  {q.get("city") ? <li><Link className={buttonClasses({ variant: "secondary", size: "sm" })} href={without(["city"])}>ابحث في كل المدن</Link></li> : null}
                  {q.get("types") ? <li><Link className={buttonClasses({ variant: "secondary", size: "sm" })} href={without(["types"])}>كل أنواع العقار</Link></li> : null}
                  {maxNow > 0 ? <li><Link className={buttonClasses({ variant: "secondary", size: "sm" })} href={without([], { maxNow: String(Math.round(maxNow * 1.2)) })}>ارفع المبلغ الآن إلى {sar(Math.round(maxNow * 1.2))} ر.س</Link></li> : null}
                  {q.get("maxInstallment") ? <li><Link className={buttonClasses({ variant: "secondary", size: "sm" })} href={without(["maxInstallment", "freq"])}>دون حد للقسط</Link></li> : null}
                  <li><Link className={buttonClasses({ variant: "text", size: "sm" })} href="/opportunities">مسح كل الفلاتر</Link></li>
                </ul>
              </>
            ) : null}
            <span className="text-14 text-muted">
              أو <Link href="/buy/new">سجّل قدرتك الشرائية</Link> ليقترح عليك الفريق الفرص المناسبة عند توفرها.
            </span>
          </div>
        ) : result ? (
          <>
            <ul className="m-0 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-2 xl:grid-cols-3">
              {result.items.map((i) => (
                <li key={i.card.reference}>
                  <OpportunityCard card={i.card} fit={i.fit} signedIn={Boolean(me)} />
                </li>
              ))}
            </ul>
            {result.pages > 1 ? (
              <nav aria-label="الصفحات" className="flex items-center justify-center gap-2">
                {page > 1 ? <Link href={link(page - 1)} className={buttonClasses({ variant: "secondary", size: "md" })}>السابق</Link> : null}
                <span className="text-14">صفحة {page} من {result.pages}</span>
                {page < result.pages ? <Link href={link(page + 1)} className={buttonClasses({ variant: "secondary", size: "md" })}>التالي</Link> : null}
              </nav>
            ) : null}
          </>
        ) : null}
        <p className="m-0 text-13 leading-6 text-muted">
          الأرقام في البطاقات تقديرية ما لم يذكر أنها راجعها الفريق، ولا تُعد عرضًا ملزمًا. القسط ربع السنوي يُقارن بمكافئه الشهري، وتظهر قيمته الفعلية ومواعيده في التفاصيل.
        </p>
      </div>
    </PublicPage>
  );
}
