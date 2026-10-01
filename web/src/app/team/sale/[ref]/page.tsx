import { notFound } from "next/navigation";
import { TeamSaleView, type TeamSaleDetail } from "@/components/market/team/TeamSaleView";
import { apiGet } from "@/lib/api/server";
import { getCatalog, marketGet } from "@/lib/market/server";

export default async function TeamSalePage({ params }: PageProps<"/team/sale/[ref]">) {
  const { ref } = await params;
  const [d, catalog, members] = await Promise.all([
    marketGet<TeamSaleDetail>(`/team/market/sale-requests/${encodeURIComponent(ref)}`),
    getCatalog(),
    apiGet<{ id: string; name: string; role: string | null }[]>("/team/market/members"),
  ]);
  if (!d) notFound();
  return <TeamSaleView d={d} catalog={catalog} members={members} />;
}
