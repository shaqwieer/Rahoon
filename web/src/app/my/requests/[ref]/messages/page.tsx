import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import { RequestMessages, type MyMessages } from "./RequestMessages";

export const generateMetadata = () => individualMetadata((c) => c.messages.title);

/** Messages with the Rahoon team for one request (D12 successor). Internal team notes are never sent here. */
export default async function MessagesPage({ params }: PageProps<"/my/requests/[ref]/messages">) {
  const { ref } = await params;
  await getIndividualContext();
  const data = await myGet<MyMessages>(`/requests/${encodeURIComponent(ref)}/messages`);
  return <RequestMessages data={data} />;
}
