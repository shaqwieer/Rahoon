import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { RaiseConcern } from "./RaiseConcern";

export const generateMetadata = () => individualMetadata((c) => c.concern.titleObjection);

/** P4: objection to data, amounts or a decision, or a complaint about the service (answered by the Rahoon team). */
export default async function ConcernPage({ params, searchParams }: PageProps<"/my/requests/[ref]/concern">) {
  const { ref } = await params;
  const sp = await searchParams;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canRaiseConcern) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  const kind = sp.kind === "complaint" ? "complaint" : "objection";
  const subject = typeof sp.subject === "string" ? sp.subject : null;
  return <RaiseConcern reference={detail.reference} kind={kind} initialSubject={subject} />;
}
