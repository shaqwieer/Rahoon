"use client";

import { useState } from "react";
import { Badge } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Icon } from "@/components/ui/Icon";
import { FileDropzone, UploadList, useUploadQueue } from "@/components/ui/Uploader";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import type { DocumentDef, PrivateDoc } from "@/lib/market/types";
import { uploadWithProgress } from "@/lib/upload/send";

const ACCEPT = "application/pdf,image/jpeg,image/png,image/heic,image/heif";

/** One document kind: what is already there, and a drop zone for one or several files (scans are compressed first). */
function KindRow({ reference, def, docs, editable, onAdded, onRemove }: {
  reference: string;
  def: DocumentDef;
  docs: PrivateDoc[];
  editable: boolean;
  onAdded: (d: PrivateDoc) => void;
  onRemove: (id: string) => void;
}) {
  const uploads = useUploadQueue<PrivateDoc>({
    purpose: "document",
    send: (file, opts) => {
      const fd = new FormData();
      fd.append("file", file);
      fd.append("kind", def.key);
      return uploadWithProgress<PrivateDoc>(`/market/sale-requests/${reference}/documents`, fd, opts);
    },
    onUploaded: onAdded,
  });
  const ok = docs.some((d) => d.reviewStatus !== "rejected");
  return (
    <li className={cn("flex flex-col gap-2.5 rounded-lg border bg-white p-3.5", ok ? "border-ok-line" : "border-line")}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="flex items-center gap-2">
          <span className={cn("flex size-8 items-center justify-center rounded-full", ok ? "bg-ok-bg text-ok" : "bg-subtle text-muted")}>
            <Icon name={ok ? "task" : "description"} size={18} />
          </span>
          <strong className="text-15">{def.label}</strong>
        </span>
        {def.publishRequired ? (ok ? <Badge tone="ok" icon="check">مرفق</Badge> : <Badge tone="warn">مطلوب قبل النشر</Badge>) : <Badge tone="neutral">اختياري</Badge>}
      </div>
      {def.help ? <span className="text-13 text-muted">{def.help}</span> : null}
      {docs.length ? (
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
          {docs.map((d) => (
            <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-subtle px-3 py-2">
              <a href={d.url} target="_blank" rel="noopener" className="flex min-w-0 items-center gap-1.5 text-14">
                <Icon name={d.contentType === "application/pdf" ? "picture_as_pdf" : "image"} size={18} />
                <span className="truncate" dir="auto">{d.fileName}</span>
              </a>
              <span className="flex items-center gap-2">
                {d.reviewStatus === "accepted" ? <Badge tone="ok">قبله الفريق</Badge> : d.reviewStatus === "rejected" ? <Badge tone="err">لم يُقبل</Badge> : <Badge tone="info">بانتظار المراجعة</Badge>}
                {editable && d.reviewStatus !== "accepted" ? (
                  <button type="button" onClick={() => onRemove(d.id)} aria-label={`حذف ${d.fileName}`} className="inline-flex size-8 items-center justify-center rounded-sm text-err hover:bg-err-bg">
                    <Icon name="delete" size={18} />
                  </button>
                ) : null}
              </span>
              {d.reviewStatus === "rejected" && d.reviewNote ? <span className="w-full text-12 text-err">{d.reviewNote}</span> : null}
            </li>
          ))}
        </ul>
      ) : null}
      {editable ? (
        <>
          <UploadList items={uploads.items} onRetry={uploads.retry} onDismiss={uploads.dismiss} />
          <FileDropzone compact accept={ACCEPT} onFiles={uploads.add} buttonLabel={docs.length ? "إضافة ملفات" : "اختيار ملفات"}
            title={docs.length ? "أضف صفحات أو نسخة أخرى" : "اسحب الملف هنا أو اختره"}
            hint="PDF أو صورة. يمكنك اختيار عدة صفحات معًا؛ نضغط الصور تلقائيًا." />
        </>
      ) : null}
    </li>
  );
}

/**
 * Private documents (contracts, statements, letters). Only the owner and the authorised Rahoon team can open them; they are
 * never part of a published opportunity. Each document kind the file needs is listed with its state.
 */
export function DocumentManager({ reference, docs: initial, defs, editable, onChanged }: {
  reference: string;
  docs: PrivateDoc[];
  defs: DocumentDef[];
  editable: boolean;
  onChanged?: (d: PrivateDoc[]) => void;
}) {
  const [docs, setDocs] = useState(initial);
  const [error, setError] = useState<string | null>(null);

  const added = (doc: PrivateDoc) =>
    setDocs((cur) => {
      const next = [...cur, doc];
      onChanged?.(next);
      return next;
    });

  const remove = async (id: string) => {
    setError(null);
    try {
      await apiSend("POST", `/market/sale-requests/${reference}/documents/${id}/remove`);
      setDocs((cur) => {
        const next = cur.filter((d) => d.id !== id);
        onChanged?.(next);
        return next;
      });
    } catch (err) {
      setError(isApiError(err) ? err.title : "تعذّر الحذف.");
    }
  };

  const kinds = [...defs.filter((d) => d.key !== "other"), defs.find((d) => d.key === "other")].filter(Boolean) as DocumentDef[];
  return (
    <div className="flex flex-col gap-3">
      <p className="m-0 flex items-start gap-2 rounded-md bg-subtle p-3 text-13 leading-6 text-charcoal">
        <Icon name="lock" size={18} className="flex-none text-muted" />
        مستنداتك خاصة: لا يراها إلا أنت وفريق رهون، ولا تظهر في أي فرصة منشورة.
      </p>
      {error ? <Alert tone="err" compact>{error}</Alert> : null}
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {kinds.map((def) => (
          <KindRow key={def.key} reference={reference} def={def} docs={docs.filter((d) => d.kind === def.key)} editable={editable} onAdded={added} onRemove={(id) => void remove(id)} />
        ))}
      </ul>
    </div>
  );
}
