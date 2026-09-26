import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { OfferView } from "./OfferView";

export const generateMetadata = () => individualMetadata((c) => c.offer.title);

/** D07 (design request D-5): the lender's offer as recorded and verified by the Rahoon team; the individual decides. */
export default async function OfferPage({ params }: PageProps<"/my/requests/[ref]/offer">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  return <OfferView detail={detail} />;
}
