import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { AcceptOffer } from "./AcceptOffer";

export const generateMetadata = () => individualMetadata((c) => c.accept.title);

/** D09: agreeing to the offer is an SMS-confirmed consent record (A-05), not a licensed signature. */
export default async function AcceptPage({ params }: PageProps<"/my/requests/[ref]/offer/accept">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canRespond || !detail.offer) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <AcceptOffer detail={detail} />;
}
