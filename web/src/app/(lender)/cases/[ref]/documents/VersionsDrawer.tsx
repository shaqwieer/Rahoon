"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { Alert, Button, DateText, Drawer, Icon, SystemState, Tag, Textarea, buttonClasses, useToast } from "@/components/ui";
import type { Tone } from "@/components/ui/tones";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { versionFileHref, type CaseDocumentItem, type DocumentVersionDto } from "@/lib/api/documents";
import { formatNumber } from "@/lib/format";
import type { DocumentPerms } from "./DocumentsView";

const REVIEW: Record<DocumentVersionDto["review"], { label: string; tone: Tone; icon: string }> = {
  Pending: { label: "قيد المراجعة", tone: "warn", icon: "pending" },
  Verified: { label: "متحقق", tone: "ok", icon: "check_circle" },
  Rejected: { label: "مرفوض", tone: "err", icon: "undo" },
};

const AUDIENCE: Record<string, string> = {
  case_team: "فريق الحالة",
  analyst: "المحلل",
  approver: "المعتمد",
  legal: "الشؤون القانونية",
  finance: "المالية",
  provider: "مقدمو الخدمة",
  owner: "المالك",
};

function fileSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${formatNumber(bytes / (1024 * 1024), 1)} م.ب` : `${formatNumber(Math.max(1, Math.round(bytes / 1024)))} ك.ب`;
}

function formatLabel(contentType: string) {
  const t = contentType.toLowerCase();
  return t.includes("pdf") ? "PDF" : t.includes("png") ? "PNG" : t.includes("jpeg") || t.includes("jpg") ? "JPG" : contentType;
}

/** L10 version history (nothing is deleted): preview / watermarked download, and verify / reject for the version under review. */
export function VersionsDrawer({ reference, doc, onClose, perms, onUploadNew }: {
  reference: string;
  doc: CaseDocumentItem | null;
  onClose: () => void;
  perms: DocumentPerms;
  onUploadNew: (doc: CaseDocumentItem) => void;
}) {
  const audience = doc ? [...doc.visibleTo.map((k) => AUDIENCE[k] ?? k), ...(doc.visibleToOwner ? ["المالك"] : [])] : [];
  return (
    <Drawer
      open={doc !== null}
      onClose={onClose}
      title={doc ? `الإصدارات · ${doc.name}` : "الإصدارات"}
      footer={
        doc ? (
          <div className="flex flex-col gap-1 text-13 text-muted">
            <span>يراه: {audience.join("، ")}.{doc.visibleToOwner ? "" : " مخفي عن المالك."}</span>
            {doc.validUntil ? (
              <span>الصلاحية: حتى <DateText value={doc.validUntil} />.</span>
            ) : null}
          </div>
        ) : undefined
      }
    >
      {doc ? <VersionsBody key={doc.id} reference={reference} doc={doc} perms={perms} onUploadNew={() => onUploadNew(doc)} /> : null}
    </Drawer>
  );
}

function VersionsBody({ reference, doc, perms, onUploadNew }: { reference: string; doc: CaseDocumentItem; perms: DocumentPerms; onUploadNew: () => void }) {
  const [state, setState] = useState<{ kind: "loading" } | { kind: "error"; message: string } | { kind: "ready"; versions: DocumentVersionDto[] }>({ kind: "loading" });

  const load = useCallback(() => {
    apiSend<DocumentVersionDto[]>("GET", `/cases/${reference}/documents/${doc.id}/versions`)
      .then((versions) => setState({ kind: "ready", versions }))
      .catch((e: unknown) => setState({ kind: "error", message: isApiError(e) && e.title ? e.title : "تعذّر تحميل الإصدارات." }));
  }, [reference, doc.id]);

  useEffect(() => {
    load();
  }, [load]);

  if (state.kind === "loading") return <div className="p-5"><SystemState kind="loading" layout="inline" /></div>;
  if (state.kind === "error") {
    return (
      <div className="flex flex-col gap-3 p-5">
        <Alert tone="err" title={state.message} />
        <Button variant="secondary" onClick={() => { setState({ kind: "loading" }); load(); }}>إعادة المحاولة</Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 p-5">
      <p className="m-0 flex items-center gap-1.5 text-13 text-muted">
        <Icon name="history" size={16} />
        لا يُحذف أي إصدار
      </p>
      {state.versions.length === 0 ? <p className="m-0 text-14 text-muted">لم يُرفع أي إصدار بعد.</p> : null}
      <ol className="m-0 flex list-none flex-col gap-3 p-0">
        {state.versions.map((v, i) => {
          const r = REVIEW[v.review];
          const href = versionFileHref(reference, v.id);
          return (
            <li key={v.id} className="flex flex-col gap-2 rounded-md border border-line p-4">
              <div className="flex flex-wrap items-center gap-2">
                <strong className="text-15"><bdi dir="ltr" className="font-mono">v{v.versionNo}</bdi>{i === 0 ? " · الحالي" : ""}</strong>
                <Tag tone={r.tone} icon={r.icon} className="ms-auto">{r.label}</Tag>
              </div>
              <span className="text-13 text-muted">
                رفعه {v.uploadedByLabel} <DateText value={v.uploadedAt} mode="datetime" /> · <bdi dir="ltr">{formatLabel(v.contentType)}</bdi> · <bdi dir="ltr">{fileSize(v.sizeBytes)}</bdi>
                {v.scan === "Pending" ? " · فحص الأمان قيد التنفيذ" : ""}
              </span>
              {v.reviewNote ? <span className="text-13">ملاحظة المراجعة: «{v.reviewNote}»</span> : null}
              {v.ownerFacingReason ? <span className="text-13">السبب المرسل للمالك: «{v.ownerFacingReason}»</span> : null}
              {perms.download ? (
                <div className="flex flex-wrap gap-2">
                  <a href={href} target="_blank" rel="noopener" className={buttonClasses({ variant: "secondary", size: "sm" })}>
                    <Icon name="visibility" size={18} />
                    معاينة
                  </a>
                  <a href={href} download={v.fileName} className={buttonClasses({ variant: "text", size: "sm" })}>
                    <Icon name="download" size={18} />
                    تنزيل بعلامة مائية
                  </a>
                </div>
              ) : null}
              {perms.review && v.review === "Pending" ? <ReviewForm reference={reference} versionId={v.id} onDone={load} /> : null}
            </li>
          );
        })}
      </ol>
      {perms.upload ? (
        <Button variant="secondary" icon="upload_file" onClick={onUploadNew} className="self-start">رفع إصدار جديد</Button>
      ) : null}
    </div>
  );
}

function ReviewForm({ reference, versionId, onDone }: { reference: string; versionId: string; onDone: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [mode, setMode] = useState<"idle" | "reject">("idle");
  const [note, setNote] = useState("");
  const [ownerReason, setOwnerReason] = useState("");
  const [busy, setBusy] = useState<"verify" | "reject" | null>(null);
  const [error, setError] = useState<{ title: string; field?: string } | null>(null);

  const submit = async (decision: "verify" | "reject") => {
    setBusy(decision);
    setError(null);
    try {
      await apiSend("POST", `/cases/${reference}/documents/versions/${versionId}/review`,
        { decision, note: note.trim() || null, ownerReason: decision === "reject" ? ownerReason.trim() : null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: decision === "verify" ? "تم التحقق من المستند." : "رُفض الإصدار وأُبلغ المالك بالسبب." });
      onDone();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e) ? { title: e.title || "تعذّر حفظ المراجعة.", field: e.fieldError("ownerReason") } : { title: "تعذّر حفظ المراجعة." });
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex flex-col gap-3 rounded-md bg-warm p-3">
      <strong className="text-14">مراجعة هذا الإصدار</strong>
      {error ? <Alert tone="err" title={error.title} /> : null}
      {mode === "reject" ? (
        <>
          <Textarea label="السبب الذي سيقرؤه المالك" requiredMark value={ownerReason} onChange={(e) => { setOwnerReason(e.target.value); key.reset(); }} maxLength={500}
            error={error?.field} help="اكتب بلغة واضحة ما المطلوب، مثل: «الصفحة الثانية غير مقروءة، نرجو تصويرها في إضاءة جيدة»." />
          <Textarea label="ملاحظة داخلية" optionalMark value={note} onChange={(e) => { setNote(e.target.value); key.reset(); }} maxLength={500} help="تظهر للفريق وفي السجل فقط." />
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" size="sm" onClick={() => setMode("idle")}>تراجع</Button>
            <Button variant="sensitive" size="sm" loading={busy === "reject"} disabled={ownerReason.trim().length < 5} onClick={() => void submit("reject")}>تأكيد الرفض</Button>
          </div>
        </>
      ) : (
        <>
          <Textarea label="ملاحظة المراجعة" optionalMark value={note} onChange={(e) => { setNote(e.target.value); key.reset(); }} maxLength={500} placeholder="مثال: مطابق لتعريف الراتب" />
          <div className="flex flex-wrap gap-2">
            <Button size="sm" icon="check_circle" loading={busy === "verify"} onClick={() => void submit("verify")}>تحقق</Button>
            <Button variant="secondary" size="sm" icon="undo" onClick={() => setMode("reject")}>رفض…</Button>
          </div>
        </>
      )}
    </div>
  );
}
