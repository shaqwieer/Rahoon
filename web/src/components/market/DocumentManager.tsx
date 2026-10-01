"use client";

import { useState } from "react";
import { Badge } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { apiSend, apiUpload, isApiError } from "@/lib/api/client";
import type { DocumentDef, PrivateDoc } from "@/lib/market/types";

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
  const [busy, setBusy] = useState<string | null>(null);
  const [failed, setFailed] = useState<{ kind: string; file: File; error: string } | null>(null);

  const upload = async (kind: string, file: File) => {
    setBusy(kind);
    setFailed(null);
    const fd = new FormData();
    fd.append("file", file);
    fd.append("kind", kind);
    try {
      const doc = await apiUpload<PrivateDoc>(`/market/sale-requests/${reference}/documents`, fd);
      setDocs((cur) => {
        const next = [...cur, doc];
        onChanged?.(next);
        return next;
      });
    } catch (err) {
      setFailed({ kind, file, error: isApiError(err) && err.title ? err.title : "تعذّر الرفع. تحقق من الاتصال." });
    } finally {
      setBusy(null);
    }
  };

  const remove = async (id: string) => {
    try {
      await apiSend("POST", `/market/sale-requests/${reference}/documents/${id}/remove`);
      setDocs((cur) => {
        const next = cur.filter((d) => d.id !== id);
        onChanged?.(next);
        return next;
      });
    } catch (err) {
      setFailed({ kind: "", file: new File([], ""), error: isApiError(err) ? err.title : "تعذّر الحذف." });
    }
  };

  const picker = (kind: string, text = "رفع") =>
    editable ? (
      <label className="inline-flex min-h-10 cursor-pointer items-center gap-1.5 rounded-sm border border-line-strong bg-white px-3 text-14 font-semibold hover:bg-subtle">
        <Icon name={busy === kind ? "progress_activity" : "upload_file"} size={18} className={busy === kind ? "animate-rh-spin" : undefined} />
        {text}
        <input type="file" accept="application/pdf,image/jpeg,image/png" className="sr-only" disabled={busy !== null}
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) void upload(kind, f);
            e.target.value = "";
          }} />
      </label>
    ) : null;

  const kinds = [...defs.filter((d) => d.key !== "other"), defs.find((d) => d.key === "other")].filter(Boolean) as DocumentDef[];
  return (
    <div className="flex flex-col gap-3">
      <p className="m-0 flex items-start gap-2 text-13 leading-6 text-muted">
        <Icon name="lock" size={18} />
        مستنداتك خاصة: لا يراها إلا أنت وفريق رهون، ولا تظهر في أي فرصة منشورة.
      </p>
      {failed ? (
        <Alert tone="err" compact>
          {failed.error}{" "}
          {failed.kind ? <Button size="sm" variant="text" onClick={() => void upload(failed.kind, failed.file)}>إعادة المحاولة</Button> : null}
        </Alert>
      ) : null}
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {kinds.map((def) => {
          const mine = docs.filter((d) => d.kind === def.key);
          const ok = mine.some((d) => d.reviewStatus !== "rejected");
          return (
            <li key={def.key} className="flex flex-col gap-2 rounded-md border border-line bg-white p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="flex items-center gap-2">
                  <Icon name={ok ? "task" : "description"} size={20} className={ok ? "text-ok" : "text-muted"} />
                  <strong className="text-15">{def.label}</strong>
                  {def.publishRequired ? (ok ? null : <Badge tone="warn">مطلوب قبل النشر</Badge>) : <Badge tone="neutral">اختياري</Badge>}
                </span>
                {picker(def.key, mine.length ? "رفع نسخة أخرى" : "رفع")}
              </div>
              {def.help ? <span className="text-13 text-muted">{def.help}</span> : null}
              {mine.map((d) => (
                <div key={d.id} className="flex flex-wrap items-center justify-between gap-2 rounded-sm bg-subtle px-3 py-2">
                  <a href={d.url} target="_blank" rel="noopener" className="flex min-w-0 items-center gap-1.5 text-14">
                    <Icon name="attach_file" size={16} />
                    <span className="truncate">{d.fileName}</span>
                  </a>
                  <span className="flex items-center gap-2">
                    {d.reviewStatus === "accepted" ? <Badge tone="ok">قبله الفريق</Badge> : d.reviewStatus === "rejected" ? <Badge tone="err">لم يُقبل</Badge> : <Badge tone="info">بانتظار المراجعة</Badge>}
                    {editable && d.reviewStatus !== "accepted" ? (
                      <button type="button" onClick={() => void remove(d.id)} className="inline-flex min-h-8 items-center rounded-sm px-2 text-12 text-err hover:bg-err-bg">
                        حذف
                      </button>
                    ) : null}
                  </span>
                  {d.reviewStatus === "rejected" && d.reviewNote ? <span className="w-full text-12 text-err">{d.reviewNote}</span> : null}
                </div>
              ))}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
