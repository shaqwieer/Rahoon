import type { Metadata } from "next";
import { teamCopy } from "@/components/team/copy";
import { apiGet } from "@/lib/api/server";
import type { ConcernQueue } from "@/lib/api/team";
import { getLocale } from "@/lib/i18n/server";
import { ObjectionsView } from "./ObjectionsView";

export async function generateMetadata(): Promise<Metadata> {
  return { title: teamCopy(await getLocale()).concerns.title };
}

/** T08 «الاعتراضات والشكاوى» (design request D-4): P4 objections and complaints, answered by the Rahoon team. */
export default async function ObjectionsPage({ searchParams }: PageProps<"/team/objections">) {
  const sp = await searchParams;
  const tab = sp.tab === "answered" ? "answered" : "open";
  const queue = await apiGet<ConcernQueue>(`/team/concerns?tab=${tab}`);
  return <ObjectionsView queue={queue} />;
}
