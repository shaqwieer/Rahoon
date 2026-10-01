import { notFound } from "next/navigation";
import { TeamOpportunityView, type TeamOpportunity } from "@/components/market/team/TeamOpportunityView";
import { marketGet } from "@/lib/market/server";

export default async function TeamOpportunityPage({ params }: PageProps<"/team/opportunities/[ref]">) {
  const { ref } = await params;
  const o = await marketGet<TeamOpportunity>(`/team/market/opportunities/${encodeURIComponent(ref)}`);
  if (!o) notFound();
  return <TeamOpportunityView key={`${o.status}-${o.versions.length}-${o.draftTerms?.id ?? ""}`} o={o} />;
}
