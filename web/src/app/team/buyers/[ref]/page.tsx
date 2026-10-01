import { notFound } from "next/navigation";
import { TeamBuyerView, type TeamBuyer } from "@/components/market/team/TeamBuyerView";
import { getCatalog, marketGet } from "@/lib/market/server";

export default async function TeamBuyerPage({ params }: PageProps<"/team/buyers/[ref]">) {
  const { ref } = await params;
  const [d, catalog] = await Promise.all([marketGet<TeamBuyer>(`/team/market/buyer-requests/${encodeURIComponent(ref)}`), getCatalog()]);
  if (!d) notFound();
  return <TeamBuyerView d={d} catalog={catalog} />;
}
