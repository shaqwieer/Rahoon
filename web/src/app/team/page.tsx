import type { Metadata } from "next";
import { teamCopy } from "@/components/team/copy";
import { apiGet } from "@/lib/api/server";
import type { TeamQueue } from "@/lib/api/team";
import { getLocale } from "@/lib/i18n/server";
import { TeamQueueView } from "./TeamQueueView";

export async function generateMetadata(): Promise<Metadata> {
  return { title: teamCopy(await getLocale()).queue.title };
}

/** T01 «الطلبات» (design request D-4): assigned to me / unassigned / all (lead) / finished, with the internal time indicator (V5). */
export default async function TeamQueuePage({ searchParams }: PageProps<"/team">) {
  const sp = await searchParams;
  const tab = typeof sp.tab === "string" ? sp.tab : "mine";
  const queue = await apiGet<TeamQueue>(`/team/requests?tab=${encodeURIComponent(tab)}`);
  return <TeamQueueView queue={queue} />;
}
