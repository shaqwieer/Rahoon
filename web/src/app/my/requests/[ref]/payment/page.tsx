import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { ReportPayment } from "./ReportPayment";

export const generateMetadata = () => individualMetadata((c) => c.execution.payment.title);

/** E03 (design request D-8): the individual reports a payment they made to their lender, with proof. */
export default async function PaymentPage({ params }: PageProps<"/my/requests/[ref]/payment">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canReportPayment) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <ReportPayment detail={detail} />;
}
