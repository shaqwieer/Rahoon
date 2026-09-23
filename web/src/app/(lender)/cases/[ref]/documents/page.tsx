import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import type { DocumentFilter, DocumentListData, DocumentTypeDto } from "@/lib/api/documents";
import { DocumentsView } from "./DocumentsView";

export const metadata: Metadata = { title: "المستندات" };

const FILTERS: DocumentFilter[] = ["all", "requested", "in_review", "expiring"];

/** L10 — Documents & requests: filter tabs with counts, versions drawer, review, request-from-owner drawer, upload. */
export default async function CaseDocumentsPage({ params, searchParams }: PageProps<"/cases/[ref]/documents">) {
  const { ref } = await params;
  const sp = await searchParams;
  const filter = (FILTERS as string[]).includes(String(sp.filter)) ? (sp.filter as DocumentFilter) : "all";
  const [list, me] = await Promise.all([apiLoad<DocumentListData>(`/cases/${encodeURIComponent(ref)}/documents?filter=${filter}`), getMe()]);
  if (!list.ok) return <LoadFailure state={list} forbiddenTitle="لا تملك صلاحية عرض مستندات هذه الحالة" />;

  const perms = me.authenticated ? me.permissions : [];
  const canRequest = perms.includes("document.request");
  const canUpload = perms.includes("document.upload");
  // The type catalogue feeds the request and upload pickers; only loaded when the role can use them.
  const types = canRequest || canUpload ? await apiLoad<DocumentTypeDto[]>("/document-types") : null;

  return (
    <DocumentsView
      reference={ref}
      filter={filter}
      data={list.data}
      types={types?.ok ? types.data : []}
      openRequestId={typeof sp.request === "string" ? sp.request : null}
      perms={{
        request: canRequest,
        upload: canUpload,
        review: perms.includes("document.review"),
        download: perms.includes("document.download"),
      }}
    />
  );
}
