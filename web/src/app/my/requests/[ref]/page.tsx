import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { RequestTracker } from "./RequestTracker";

export const generateMetadata = () => individualMetadata((c) => c.tracker.eyebrow);

/** Request home (D-3 / OA06 successor, D02 rework): status, «ننتظر», next step, «ماذا ستفعل رهون لك», past timeline. */
export default async function RequestPage({ params }: PageProps<"/my/requests/[ref]">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (detail.canEdit) redirect(`/my/requests/${encodeURIComponent(detail.reference)}/apply?step=1`);
  return <RequestTracker detail={detail} />;
}
