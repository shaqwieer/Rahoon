import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { Submitted } from "./Submitted";

export const generateMetadata = () => individualMetadata((c) => c.submitted.title);

/** D-3 frame 1: submission confirmation — reference and what happens next (no dates, Q6). */
export default async function SubmittedPage({ params }: PageProps<"/my/requests/[ref]/submitted">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  return <Submitted reference={detail.reference} />;
}
