"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, Dialog, Icon, Select, UploadDropzone, useToast } from "@/components/ui";
import { apiUpload, isApiError } from "@/lib/api/client";
import type { CaseDocumentItem, DocumentTypeDto } from "@/lib/api/documents";

/** Internal upload (multipart): a new document of a chosen type, or a new version of an existing one. Content type is sniffed by the API. */
export function UploadDialog({ reference, target, types, onClose }: {
  reference: string;
  target: CaseDocumentItem | "new" | null;
  types: DocumentTypeDto[];
  onClose: () => void;
}) {
  return (
    <Dialog
      open={target !== null}
      onClose={onClose}
      title={target && target !== "new" ? `رفع إصدار جديد · ${target.name}` : "رفع مستند"}
      description="PDF أو JPG أو PNG حتى 20 م.ب. يُنشأ إصدار جديد دون حذف السابق، ويُفحص الملف قبل قبوله."
    >
      {target !== null ? <UploadForm key={target === "new" ? "new" : target.id} reference={reference} target={target} types={types} onClose={onClose} /> : null}
    </Dialog>
  );
}

function UploadForm({ reference, target, types, onClose }: { reference: string; target: CaseDocumentItem | "new"; types: DocumentTypeDto[]; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const [typeKey, setTypeKey] = useState(target === "new" ? "" : target.documentTypeKey);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; file?: string; type?: string } | null>(null);

  const upload = async () => {
    if (!file || !typeKey) return;
    setBusy(true);
    setError(null);
    const form = new FormData();
    form.append("file", file);
    form.append("documentTypeKey", typeKey);
    if (target !== "new") form.append("documentId", target.id);
    try {
      const res = await apiUpload<{ version: number; scan: string }>(`/cases/${reference}/documents`, form);
      toast.toast({ tone: "ok", message: `رُفع الإصدار v${res.version} وهو الآن قيد المراجعة.` });
      onClose();
      router.refresh();
    } catch (e) {
      setError(isApiError(e)
        ? { title: e.title || "تعذّر رفع الملف.", file: e.fieldError("file"), type: e.fieldError("documentTypeKey") }
        : { title: "تعذّر رفع الملف." });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-4 p-5">
      {error ? <Alert tone="err" title={error.title} /> : null}
      {target === "new" ? (
        <Select label="نوع المستند" requiredMark placeholder="اختر نوع المستند" value={typeKey} onChange={(e) => setTypeKey(e.target.value)}
          options={types.map((t) => ({ value: t.key, label: t.nameAr }))} error={error?.type} />
      ) : null}
      <UploadDropzone onFiles={(f) => { setFile(f[0] ?? null); setError(null); }} disabled={busy} />
      {file ? (
        <p className="m-0 flex items-center gap-2 text-14">
          <Icon name="description" size={18} />
          <bdi dir="ltr">{file.name}</bdi>
        </p>
      ) : null}
      {error?.file ? <span className="text-13 text-err">{error.file}</span> : null}
      <div className="flex flex-wrap gap-2.5">
        <Button variant="secondary" onClick={onClose}>إلغاء</Button>
        <Button onClick={() => void upload()} loading={busy} disabled={!file || !typeKey}>رفع</Button>
      </div>
    </div>
  );
}
