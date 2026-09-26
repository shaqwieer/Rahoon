import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import type { MyRequestDetail } from "@/lib/api/requests";
import { PathsView } from "./PathsView";

export const generateMetadata = () => individualMetadata((c) => c.pathsPage.title);

/** D06 successor (design request D-5): the four help paths — never «متاح لك», never a guarantee (Q11). */
export default async function PathsPage({ params }: PageProps<"/my/requests/[ref]/paths">) {
  const { ref } = await params;
  await getIndividualContext();
  const detail = await myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`);
  return <PathsView detail={detail} />;
}
