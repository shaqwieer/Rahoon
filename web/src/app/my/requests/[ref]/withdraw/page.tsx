import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { WithdrawRequest } from "./WithdrawRequest";

export const generateMetadata = () => individualMetadata((c) => c.withdraw.title);

export default async function WithdrawPage({ params }: PageProps<"/my/requests/[ref]/withdraw">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canWithdraw) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <WithdrawRequest detail={detail} />;
}
