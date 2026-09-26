import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import type { TeamRequestDetail } from "@/lib/api/team";
import { TeamRequestView } from "./TeamRequestView";

export async function generateMetadata({ params }: PageProps<"/team/requests/[ref]">): Promise<Metadata> {
  return { title: (await params).ref, referrer: "no-referrer" };
}

/** T02 «مراجعة الطلب» + T03/T04 drawers (design request D-4, «بانتظار اعتماد التصميم»). */
export default async function TeamRequestPage({ params }: PageProps<"/team/requests/[ref]">) {
  const { ref } = await params;
  const detail = await apiGet<TeamRequestDetail>(`/team/requests/${encodeURIComponent(ref)}`);
  return <TeamRequestView detail={detail} />;
}
