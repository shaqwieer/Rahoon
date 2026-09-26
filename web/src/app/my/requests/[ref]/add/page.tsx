import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { AddInfo } from "./AddInfo";

export const generateMetadata = () => individualMetadata((c) => c.add.title);

/** «إضافة معلومة» — after submission the request is add-only (text and documents). */
export default async function AddInfoPage({ params }: PageProps<"/my/requests/[ref]/add">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  if (!detail.canAddInfo) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  return <AddInfo detail={detail} />;
}
