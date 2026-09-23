import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerDocuments } from "@/components/owner/types";
import { EmptyState } from "@/components/ui";
import { DocumentsClient } from "./DocumentsClient";

export const generateMetadata = () => ownerMetadata((c) => c.docs.title);

/** D04 — requested documents: rejected first with the reason and fix advice, upload/camera/help per request. */
export default async function OwnerDocumentsPage() {
  const { c, shell } = await getOwnerContext();
  const data = await ownerGet<OwnerDocuments>("/documents");
  const rejected = data.items.filter((d) => d.rawStatus === "Rejected").length;
  const sub = rejected > 0 ? c.docs.subRejected(rejected) : data.needsAction > 0 ? c.docs.subNeeded(data.needsAction) : data.items.length ? c.docs.subDone : undefined;
  return (
    <OwnerPage shell={shell} title={c.docs.title} sub={sub} active="docs">
      {data.items.length === 0 ? <EmptyState icon="folder" title={c.docs.emptyTitle} body={c.docs.emptyBody} /> : <DocumentsClient items={data.items} />}
    </OwnerPage>
  );
}
