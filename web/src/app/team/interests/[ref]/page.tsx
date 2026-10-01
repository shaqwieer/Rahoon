import { notFound } from "next/navigation";
import { TeamInterestView, type TeamInterest } from "@/components/market/team/TeamInterestView";
import { marketGet } from "@/lib/market/server";

export default async function TeamInterestPage({ params }: PageProps<"/team/interests/[ref]">) {
  const { ref } = await params;
  const i = await marketGet<TeamInterest>(`/team/market/interests/${encodeURIComponent(ref)}`);
  if (!i) notFound();
  return <TeamInterestView i={i} />;
}
