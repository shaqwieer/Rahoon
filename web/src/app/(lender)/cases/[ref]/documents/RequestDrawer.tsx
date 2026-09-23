"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Alert, Button, Checkbox, DateField, Drawer, Icon, RadioCardGroup, Select, Skeleton, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { DocumentRequestPreview, DocumentTypeDto } from "@/lib/api/documents";

/** Riyadh calendar date `days` from today, as yyyy-MM-dd. */
function riyadhDatePlus(days: number) {
  const d = new Date(Date.now() + days * 86_400_000);
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Riyadh", year: "numeric", month: "2-digit", day: "2-digit" }).format(d);
}

interface PreviewError {
  title: string;
  dueOn?: string;
  documentTypeKey?: string;
}

const CHANNELS = [
  { key: "portal", label: "داخل البوابة" },
  { key: "sms", label: "إشعار نصي" },
] as const;

/**
 * L10 request drawer: type (institution rule shown), who uploads, due date (Gregorian + Hijri + days), and the exact
 * owner-facing text rendered by the API from the published template — previewed before anything is sent.
 */
export function RequestDrawer({ reference, open, onClose, types, initialTypeKey, canUpload, onUploadInstead }: {
  reference: string;
  open: boolean;
  onClose: () => void;
  types: DocumentTypeDto[];
  initialTypeKey: string | null;
  canUpload: boolean;
  onUploadInstead: () => void;
}) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [typeKey, setTypeKey] = useState(initialTypeKey ?? "");
  const [uploader, setUploader] = useState<"owner" | "internal">("owner");
  const [dueOn, setDueOn] = useState(() => riyadhDatePlus(10));
  const [channels, setChannels] = useState<string[]>(["portal", "sms"]);
  const [result, setResult] = useState<{ key: string; preview: DocumentRequestPreview | null; error: PreviewError | null } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const inputKey = JSON.stringify([typeKey, uploader, dueOn, channels]);
  const wantsPreview = open && !!typeKey && !!dueOn;

  // Live preview: the API renders the owner-facing text from the published template (nothing is saved or sent).
  useEffect(() => {
    if (!wantsPreview) return;
    const ctrl = new AbortController();
    apiSend<DocumentRequestPreview>("POST", `/cases/${reference}/documents/requests/preview`, {
      documentTypeKey: typeKey,
      dueOn,
      uploader,
      channels: uploader === "owner" && channels.length > 0 ? channels : null,
    }, { signal: ctrl.signal })
      .then((p) => setResult({ key: inputKey, preview: p, error: null }))
      .catch((e: unknown) => {
        if (e instanceof DOMException && e.name === "AbortError") return;
        setResult({
          key: inputKey,
          preview: null,
          error: isApiError(e)
            ? { title: e.title || "تعذّرت المعاينة.", dueOn: e.fieldError("dueOn"), documentTypeKey: e.fieldError("documentTypeKey") }
            : { title: "تعذّرت المعاينة." },
        });
      });
    return () => ctrl.abort();
  }, [wantsPreview, reference, typeKey, dueOn, uploader, channels, inputKey]);

  const fresh = result?.key === inputKey ? result : null;
  const preview = fresh?.preview ?? null;
  const previewError = fresh?.error ?? null;
  const previewing = wantsPreview && !fresh;

  const edited = () => {
    key.reset();
    setError(null);
  };

  const send = async () => {
    if (!preview || uploader !== "owner") return;
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/${reference}/documents/requests`, { documentTypeKey: typeKey, dueOn, ownerMessage: preview.ownerMessage, channels }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُرسل الطلب للمالك وسُجّل في سجل الحالة." });
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e) ? e.title || "تعذّر إرسال الطلب." : "تعذّر إرسال الطلب.");
    } finally {
      setBusy(false);
    }
  };

  const ready = uploader === "owner" && !!preview && !previewing && channels.length > 0;

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="طلب مستند"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          {uploader === "owner" ? (
            <Button className="flex-1" onClick={() => void send()} loading={busy} softDisabled={!ready} aria-describedby="req-hint">إرسال الطلب</Button>
          ) : canUpload ? (
            <Button className="flex-1" icon="upload_file" onClick={onUploadInstead}>رفع المستند داخلياً</Button>
          ) : null}
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error} /> : null}
        <Select
          label="نوع المستند"
          requiredMark
          placeholder="اختر نوع المستند"
          value={typeKey}
          onChange={(e) => { setTypeKey(e.target.value); edited(); }}
          options={types.map((t) => ({ value: t.key, label: t.nameAr }))}
          help={preview?.rule.helper}
          error={previewError?.documentTypeKey}
        />
        <RadioCardGroup
          legend="من يرفعه"
          name="uploader"
          columns={1}
          value={uploader}
          onChange={(v) => { setUploader(v as "owner" | "internal"); edited(); }}
          options={[
            { value: "owner", label: "المالك (عبر بوابة المالك)" },
            { value: "internal", label: "فريق المصرف (داخلي)" },
          ]}
        />
        <DateField
          label="المهلة"
          requiredMark
          value={dueOn}
          onValueChange={(v) => { setDueOn(v); edited(); }}
          min={riyadhDatePlus(1)}
          help={preview ? preview.dueLabel : undefined}
          error={previewError?.dueOn}
        />
        {uploader === "owner" ? (
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="mb-1 text-14 font-semibold">قنوات الإشعار</legend>
            {CHANNELS.map((c) => (
              <Checkbox
                key={c.key}
                label={c.label}
                checked={channels.includes(c.key)}
                onChange={(e) => { setChannels(e.target.checked ? [...channels, c.key] : channels.filter((x) => x !== c.key)); edited(); }}
              />
            ))}
            {channels.length === 0 ? <span className="text-13 text-err">اختر قناة واحدة على الأقل.</span> : null}
          </fieldset>
        ) : null}

        <section aria-labelledby="req-prev-h" aria-live="polite" className="flex flex-col gap-2 rounded-md border border-line bg-warm p-4">
          <h3 id="req-prev-h" className="m-0 text-14 font-semibold">معاينة ما سيراه المالك</h3>
          {previewing ? (
            <div className="flex flex-col gap-2"><Skeleton width="90%" height={14} /><Skeleton width="70%" height={14} /></div>
          ) : preview ? (
            <>
              {preview.ownerMessage ? <p className="m-0 text-15 leading-7">{preview.ownerMessage}</p> : null}
              <span className="text-12 text-muted">{preview.caption}</span>
            </>
          ) : previewError ? (
            <span className="text-13 text-err">{previewError.title}</span>
          ) : (
            <span className="text-13 text-muted">اختر نوع المستند والمهلة لعرض النص الذي سيصل للمالك.</span>
          )}
        </section>
        <p id="req-hint" className="m-0 flex items-start gap-1.5 text-13 text-muted">
          <Icon name="info" size={16} />
          {uploader === "internal"
            ? "الطلب الداخلي لا يُرسل شيئاً للمالك؛ ارفع المستند مباشرة من فريق المصرف."
            : ready
              ? "يُرسل النص أعلاه كما هو، ويُسجَّل الطلب في سجل الحالة."
              : "لا يُرسل الطلب قبل معاينة النص الذي سيصل للمالك."}
        </p>
      </div>
    </Drawer>
  );
}
