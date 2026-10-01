import type { Metadata } from "next";
import { Suspense } from "react";
import { DiscoveryView } from "@/components/market/discovery/DiscoveryView";
import { apiFetch } from "@/lib/api/server";
import { parseResponse } from "@/lib/api/types";
import { getCatalog, optionalIndividual } from "@/lib/market/server";
import { fromParams, hasFilters, PAGE_SIZE, toQuery } from "@/lib/market/search";
import type { SearchResult } from "@/lib/market/types";
import { PublicPage } from "../PublicPage";

function paramsOf(sp: Record<string, string | string[] | undefined>) {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(sp)) if (typeof v === "string" && v) q.set(k, v);
  return q;
}

/** The unfiltered list is indexable; filtered, paged or personal («match=me») URLs are not, and point to the list. */
export async function generateMetadata({ searchParams }: PageProps<"/opportunities">): Promise<Metadata> {
  const state = fromParams(paramsOf(await searchParams));
  const plain = !hasFilters(state) && !state.page && !state.sort;
  return {
    title: "الفرص المتاحة",
    description: "فرص عقارية في السعودية تعرض المبلغ المطلوب منك الآن منفصلًا عن سعر الشراء والرصيد والأقساط، مع توضيح ما تم التحقق منه.",
    alternates: { canonical: "/opportunities" },
    robots: plain ? { index: true, follow: true } : { index: false, follow: true },
  };
}

/** Published opportunities: list and map from the same server filters; the first page is rendered here, the rest client-side. */
export default async function OpportunitiesPage({ searchParams }: PageProps<"/opportunities">) {
  const state = fromParams(paramsOf(await searchParams));
  const query = toQuery(state);
  const [catalog, me] = await Promise.all([getCatalog(), optionalIndividual()]);
  let initial: SearchResult | null = null;
  try {
    initial = await parseResponse<SearchResult>(await apiFetch(`/market/opportunities?${query ? `${query}&` : ""}pageSize=${PAGE_SIZE}`));
  } catch {
    initial = null;
  }
  return (
    <PublicPage wide title="الفرص المتاحة" lead="اعثر على فرصة تناسب ميزانيتك بأرقام واضحة: المطلوب منك الآن، ثم ما تلتزم به لاحقًا — في قائمة أو على الخريطة.">
      <Suspense fallback={<div className="h-48 animate-rh-pulse rounded-lg bg-subtle" />}>
        <DiscoveryView catalog={catalog} initial={initial} initialQuery={query} signedIn={Boolean(me)} />
      </Suspense>
    </PublicPage>
  );
}
