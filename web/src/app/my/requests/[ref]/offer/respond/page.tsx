import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { RespondOffer } from "./RespondOffer";

export const generateMetadata = () => individualMetadata((c) => c.respond.questionTitle);

/** D08 (question / suggestion) and «لا يناسبني» (decline, no reason required; no action against the individual). */
export default async function RespondPage({ params, searchParams }: PageProps<"/my/requests/[ref]/offer/respond">) {
  const { ref } = await params;
  const sp = await searchParams;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canRespond) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <RespondOffer detail={detail} decline={sp.kind === "decline"} />;
}
