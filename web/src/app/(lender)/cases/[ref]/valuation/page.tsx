import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { Tabs } from "@/components/ui";
import type { DocumentListData } from "@/lib/api/documents";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import { INCOME_DOCUMENT_TYPES, type AnalysisData, type ValuationData } from "@/lib/api/valuation";
import { AnalysisView, type IncomeDocOption } from "./AnalysisView";
import { ValuationView } from "./ValuationView";

export const metadata: Metadata = { title: "التقييم والتحليل" };

/** L11 (التقييم) + L12 (التحليل) — one case tab with two sub-tabs, as designed; `?view=analysis` selects L12. */
export default async function CaseValuationPage({ params, searchParams }: PageProps<"/cases/[ref]/valuation">) {
  const { ref } = await params;
  const sp = await searchParams;
  const view = sp.view === "analysis" ? "analysis" : "valuation";
  const base = `/cases/${ref}/valuation`;
  const me = await getMe();
  const perms = me.authenticated ? me.permissions : [];
  const enc = encodeURIComponent(ref);

  let body: React.ReactNode;
  if (view === "valuation") {
    const data = await apiLoad<ValuationData>(`/cases/${enc}/valuation`);
    body = data.ok ? (
      <ValuationView
        reference={ref}
        data={data.data}
        canAssign={perms.includes("valuation.assign")}
        canDownload={perms.includes("document.download")}
      />
    ) : (
      <LoadFailure state={data} forbiddenTitle="لا تملك صلاحية عرض التقييم" />
    );
  } else {
    const data = await apiLoad<AnalysisData>(`/cases/${enc}/analysis`);
    // Income may only come from a verified salary / bank statement version on this case (the API enforces it).
    let incomeDocs: IncomeDocOption[] = [];
    if (data.ok && data.data.canEdit) {
      const docs = await apiLoad<DocumentListData>(`/cases/${enc}/documents?filter=all`);
      if (docs.ok) {
        incomeDocs = docs.data.items
          .filter((d) => INCOME_DOCUMENT_TYPES.includes(d.documentTypeKey) && d.currentVersionId && d.currentReview === "Verified")
          .map((d) => ({ versionId: d.currentVersionId!, label: `${d.name} ${d.version}` }));
      }
    }
    body = data.ok ? (
      <AnalysisView key={data.data.version ?? "new"} data={data.data} reference={ref} incomeDocs={incomeDocs} />
    ) : (
      <LoadFailure state={data} forbiddenTitle="لا تملك صلاحية عرض التحليل" />
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="m-0 flex-1 text-20 font-bold">التقييم والتحليل</h2>
      </div>
      <Tabs
        label="التقييم والتحليل"
        active={view}
        tabs={[
          { key: "valuation", label: "التقييم", href: base },
          { key: "analysis", label: "التحليل", href: `${base}?view=analysis` },
        ]}
      />
      {body}
    </div>
  );
}
