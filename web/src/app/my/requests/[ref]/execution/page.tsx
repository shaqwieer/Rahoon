import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { ExecutionDetail } from "../ExecutionViews";

export const generateMetadata = () => individualMetadata((c) => c.execution.title);

/** E02/E04/E05 (design request D-8): the agreement and the lender's schedule, confirmations, reports, notices, closure documents. */
export default async function ExecutionPage({ params, searchParams }: PageProps<"/my/requests/[ref]/execution">) {
  const { ref } = await params;
  const reported = (await searchParams).reported === "1";
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.execution) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <ExecutionDetail detail={detail} reported={reported} />;
}
