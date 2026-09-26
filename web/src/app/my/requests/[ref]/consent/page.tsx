import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { NewConsent } from "./NewConsent";

export const generateMetadata = () => individualMetadata((c) => c.consent.title);

/** Renewing consent after it was withdrawn or never recorded (same OA04 consent box, SMS-confirmed). */
export default async function ConsentPage({ params }: PageProps<"/my/requests/[ref]/consent">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (detail.canEdit) redirect(`/my/requests/${encodeURIComponent(detail.reference)}/apply?step=4`);
  return <NewConsent detail={detail} />;
}
