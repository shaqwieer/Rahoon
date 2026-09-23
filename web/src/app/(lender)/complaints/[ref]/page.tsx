import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { PageHeader } from "@/components/shell/PageHeader";
import type { ComplaintDetail } from "@/lib/api/complaints";
import { apiLoad } from "@/lib/api/load";
import { ComplaintView } from "./ComplaintView";

export async function generateMetadata({ params }: PageProps<"/complaints/[ref]">): Promise<Metadata> {
  const { ref } = await params;
  return { title: ref };
}

/** L23 — Complaint under independent review: text, findings, decision & written response; tag-only for other roles (API-shaped). */
export default async function ComplaintPage({ params }: PageProps<"/complaints/[ref]">) {
  const { ref } = await params;
  const res = await apiLoad<ComplaintDetail>(`/complaints/${encodeURIComponent(ref)}`);
  if (!res.ok) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title={<bdi dir="ltr" className="font-mono">{ref}</bdi>} />
        <LoadFailure state={res} forbiddenTitle="لا تملك صلاحية عرض الشكوى" forbiddenBody="تظهر الشكاوى لمراجعي الامتثال ولمن يملك صلاحية «عرض الشكاوى»." />
      </div>
    );
  }
  return <ComplaintView key={res.data.reference} d={res.data} />;
}
