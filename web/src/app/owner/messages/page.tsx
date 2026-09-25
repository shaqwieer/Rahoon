import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerMessages } from "@/components/owner/types";
import { MessagesClient } from "./MessagesClient";

export const generateMetadata = () => ownerMetadata((c) => c.messages.title);

/** D12 — messages with the case manager (owner channel only; never internal notes) and appointments. */
export default async function OwnerMessagesPage() {
  const { c, shell } = await getOwnerContext();
  const data = await ownerGet<OwnerMessages>("/messages");
  return (
    <OwnerPage shell={shell} title={c.messages.title} sub={c.messages.sub(data.with, shell.lenderName)} backHref="/owner" backLabel={c.backHome} hideNav active="home">
      <MessagesClient appointments={data.appointments} messages={data.messages} />
    </OwnerPage>
  );
}
