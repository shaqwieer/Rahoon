import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import type { PartiesData } from "@/lib/api/caseInfo";
import { apiLoad } from "@/lib/api/load";
import { PartiesView } from "./PartiesView";

export const metadata: Metadata = { title: "الأطراف" };

/** L06 — Parties: masked party cards with audited reveal, contact preferences, owner invitation, POA requests. */
export default async function PartiesPage({ params }: PageProps<"/cases/[ref]/parties">) {
  const { ref } = await params;
  const res = await apiLoad<PartiesData>(`/cases/${encodeURIComponent(ref)}/parties`);
  if (!res.ok) return <LoadFailure state={res} forbiddenTitle="لا تملك صلاحية عرض أطراف هذه الحالة" />;
  return <PartiesView reference={ref} data={res.data} />;
}
