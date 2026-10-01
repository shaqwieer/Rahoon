import type { Metadata } from "next";
import { CompareView } from "@/components/market/discovery/CompareView";
import { marketGet } from "@/lib/market/server";
import type { CompareResult } from "@/lib/market/types";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = { title: "مقارنة الفرص", robots: { index: false, follow: false } };

/** Up to four opportunities side by side (the selection lives in the URL and this browser). */
export default async function ComparePage({ searchParams }: PageProps<"/compare">) {
  const raw = (await searchParams).refs;
  const refs = (typeof raw === "string" ? raw : "").split(",").map((r) => r.trim()).filter((r) => r && r.length <= 20).slice(0, 4);
  const result = refs.length
    ? ((await marketGet<CompareResult>(`/market/compare?refs=${encodeURIComponent(refs.join(","))}`)) ?? { items: [], max: 4, hasProfile: false })
    : { items: [], max: 4, hasProfile: false };
  return (
    <PublicPage wide title="مقارنة الفرص" lead="المطلوب الآن والالتزام اللاحق والأقساط والدفعات الإضافية جنبًا إلى جنب، مع ما راجعه الفريق وما لا ينطبق على كل طريق.">
      <CompareView result={result} refs={refs} />
    </PublicPage>
  );
}
