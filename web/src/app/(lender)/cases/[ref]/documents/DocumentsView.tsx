"use client";

import { useState } from "react";
import { Button, DocumentItem, DocumentList, EmptyState, Icon, Tabs, type DocumentStatus } from "@/components/ui";
import type { CaseDocumentItem, DocumentFilter, DocumentListData, DocumentTypeDto } from "@/lib/api/documents";
import { RequestDrawer } from "./RequestDrawer";
import { UploadDialog } from "./UploadDialog";
import { VersionsDrawer } from "./VersionsDrawer";

export interface DocumentPerms {
  request: boolean;
  upload: boolean;
  review: boolean;
  download: boolean;
}

/** Maps the API status (raw status + tone + icon) onto the C07 DocumentItem status; the API text is shown verbatim. */
export function toDocumentStatus(d: Pick<CaseDocumentItem, "rawStatus" | "tone" | "statusIcon" | "documentTypeKey">): DocumentStatus {
  switch (d.rawStatus) {
    case "Verified":
      return d.tone === "err" ? "expired" : d.tone === "warn" ? "expiring" : d.statusIcon === "event_available" || d.documentTypeKey === "valuation_report" ? "valid" : "verified";
    case "Rejected":
      return d.statusIcon === "undo" ? "returned" : "rejected";
    case "Requested":
      return "required";
    case "Uploaded":
    case "InReview":
      return "under_review";
    case "Expired":
      return "expired";
    default:
      return "not_requested";
  }
}

const EMPTY: Record<DocumentFilter, { title: string; body: string }> = {
  all: { title: "لا توجد مستندات على هذه الحالة بعد", body: "اطلب المستند من المالك أو ارفعه من فريق المصرف." },
  requested: { title: "لا مستندات مطلوبة", body: "كل الطلبات المفتوحة استُكملت." },
  in_review: { title: "لا مستندات قيد المراجعة", body: "ستظهر هنا الإصدارات المرفوعة التي تنتظر التحقق." },
  expiring: { title: "لا مستندات تنتهي قريباً", body: "تظهر هنا المستندات المتحقق منها التي تنتهي صلاحيتها خلال 30 يوماً." },
};

export function DocumentsView({ reference, filter, data, types, openRequestId, perms }: {
  reference: string;
  filter: DocumentFilter;
  data: DocumentListData;
  types: DocumentTypeDto[];
  /** `?request=new` opens an empty request; `?request={documentId}` (from the overview «طلب تحديث») prefills that document's type. */
  openRequestId: string | null;
  perms: DocumentPerms;
}) {
  const base = `/cases/${reference}/documents`;
  const prefillDoc = openRequestId && openRequestId !== "new" ? data.items.find((d) => d.id === openRequestId) : undefined;
  const [requestOpen, setRequestOpen] = useState(perms.request && openRequestId !== null);
  const [versionsOf, setVersionsOf] = useState<CaseDocumentItem | null>(null);
  const [uploadFor, setUploadFor] = useState<CaseDocumentItem | "new" | null>(null);
  const c = data.counts;

  const actionFor = (d: CaseDocumentItem) => {
    if (d.versionCount > 0) {
      const review = perms.review && d.currentReview === "Pending";
      return { label: review ? "مراجعة" : "الإصدارات", onClick: () => setVersionsOf(d) };
    }
    if (perms.upload) return { label: "رفع", onClick: () => setUploadFor(d) };
    return undefined;
  };

  return (
    <section aria-labelledby="docs-h" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 id="docs-h" className="m-0 flex-1 text-20 font-bold">المستندات والطلبات</h2>
        {perms.upload ? (
          <Button variant="secondary" icon="upload_file" onClick={() => setUploadFor("new")}>رفع مستند</Button>
        ) : null}
        {perms.request ? (
          <Button icon="add" onClick={() => setRequestOpen(true)}>طلب مستند</Button>
        ) : null}
      </div>

      <Tabs
        label="تصفية المستندات"
        active={filter}
        tabs={[
          { key: "all", label: "الكل", count: c.all, href: base },
          { key: "requested", label: "مطلوبة", count: c.requested, href: `${base}?filter=requested` },
          { key: "in_review", label: "قيد المراجعة", count: c.inReview, href: `${base}?filter=in_review` },
          { key: "expiring", label: "تنتهي قريباً", count: c.expiring, countTone: c.expiring > 0 ? "err" : "neutral", href: `${base}?filter=expiring` },
        ]}
      />

      {data.items.length === 0 ? (
        <EmptyState icon="folder_open" title={EMPTY[filter].title} body={EMPTY[filter].body} />
      ) : (
        <DocumentList>
          {data.items.map((d) => (
            <DocumentItem
              key={d.id}
              icon={d.icon}
              name={d.name}
              version={d.version !== "—" ? d.version : undefined}
              status={toDocumentStatus(d)}
              statusLabel={d.statusText}
              action={actionFor(d)}
              meta={
                <>
                  <span>{d.meta}</span>
                  {!d.visibleToOwner ? (
                    <span className="ms-2 inline-flex items-center gap-1 whitespace-nowrap">
                      <Icon name="visibility_off" size={14} />
                      لا يراه المالك
                    </span>
                  ) : null}
                </>
              }
            />
          ))}
        </DocumentList>
      )}

      <p className="m-0 flex items-start gap-1.5 text-13 text-muted">
        <Icon name="info" size={16} />
        لا يُحذف أي إصدار. كل رفع ينشئ إصداراً جديداً، والرفض يتطلب سبباً يُرسل للمالك بلغة واضحة. التنزيل بعلامة مائية باسمك والوقت، ويُسجَّل.
      </p>

      {perms.request ? (
        <RequestDrawer
          reference={reference}
          open={requestOpen}
          onClose={() => setRequestOpen(false)}
          types={types}
          initialTypeKey={prefillDoc?.documentTypeKey ?? null}
          canUpload={perms.upload}
          onUploadInstead={() => {
            setRequestOpen(false);
            setUploadFor("new");
          }}
        />
      ) : null}
      <VersionsDrawer
        reference={reference}
        doc={versionsOf}
        onClose={() => setVersionsOf(null)}
        perms={perms}
        onUploadNew={(d) => {
          setVersionsOf(null);
          setUploadFor(d);
        }}
      />
      {perms.upload ? (
        <UploadDialog reference={reference} target={uploadFor} types={types} onClose={() => setUploadFor(null)} />
      ) : null}
    </section>
  );
}
