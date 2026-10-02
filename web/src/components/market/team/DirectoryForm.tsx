"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ActionDialog, ActionError } from "@/components/market/team/useTeamAction";
import { Badge, Card } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, Textarea, DateField, TextField } from "@/components/ui/Field";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { ORG_TYPE_LABELS } from "@/lib/market/directory";

export interface DirectoryRecord {
  id: string;
  nameAr: string;
  nameEn: string | null;
  types: string[];
  website: string | null;
  licenseNumber: string | null;
  registrationNumber: string | null;
  sourceName: string | null;
  sourceUrl: string | null;
  verifiedOn: string | null;
  active: boolean;
  version: number;
}

type Values = { nameAr: string; nameEn: string; types: string[]; website: string; licenseNumber: string; registrationNumber: string; sourceName: string; sourceUrl: string; verifiedOn: string };

const TYPES = ["developer", "bank", "finance_company"];

const toValues = (r?: DirectoryRecord): Values => ({
  nameAr: r?.nameAr ?? "", nameEn: r?.nameEn ?? "", types: r?.types ?? [], website: r?.website ?? "", licenseNumber: r?.licenseNumber ?? "",
  registrationNumber: r?.registrationNumber ?? "", sourceName: r?.sourceName ?? "", sourceUrl: r?.sourceUrl ?? "", verifiedOn: r?.verifiedOn ?? "",
});

/**
 * Add or edit a directory organization. Saving marks the record as edited by an administrator: later imports never
 * change it again. Identifiers are entered only with the official source that publishes them.
 */
export function DirectoryForm({ record }: { record?: DirectoryRecord }) {
  const router = useRouter();
  const key = useIdempotencyKey();
  const [v, setV] = useState<Values>(toValues(record));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const set = (p: Partial<Values>) => {
    setSaved(false);
    setV((x) => ({ ...x, ...p }));
  };

  const submit = async () => {
    const e: Record<string, string> = {};
    if (v.nameAr.trim().length < 2) e.nameAr = "اكتب الاسم العربي للجهة.";
    if (v.types.length === 0) e.types = "اختر نوعًا واحدًا على الأقل.";
    setErrors(e);
    setFailure(null);
    if (Object.keys(e).length) return;
    setBusy(true);
    const body = {
      nameAr: v.nameAr, nameEn: v.nameEn || null, types: v.types, website: v.website || null, licenseNumber: v.licenseNumber || null,
      registrationNumber: v.registrationNumber || null, sourceName: v.sourceName || null, sourceUrl: v.sourceUrl || null, verifiedOn: v.verifiedOn || null,
      version: record?.version,
    };
    try {
      const res = await apiSend<DirectoryRecord>(record ? "PUT" : "POST", record ? `/team/directory/${record.id}` : "/team/directory", body, { idempotencyKey: key.get() });
      key.reset();
      if (record) {
        setSaved(true);
        router.refresh();
      } else {
        router.push(`/team/organizations/${res.id}`);
      }
    } catch (err) {
      if (isApiError(err)) {
        key.reset();
        setErrors(Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, x]) => [k, x[0]])));
        setFailure(err.title || "تعذّر الحفظ.");
      } else setFailure("تعذّر الاتصال. أعد المحاولة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card title={record ? "تعديل البيانات" : "بيانات الجهة"}>
      <form
        className="flex flex-col gap-4"
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        {failure ? <Alert tone="err" title={failure} /> : null}
        {saved ? <Alert tone="ok" title="حُفظت التعديلات. لن يغيّرها الاستيراد لاحقًا." /> : null}
        <TextField label="الاسم بالعربية" value={v.nameAr} onChange={(e) => set({ nameAr: e.target.value })} error={errors.nameAr} requiredMark />
        <TextField label="الاسم بالإنجليزية" value={v.nameEn} onChange={(e) => set({ nameEn: e.target.value })} error={errors.nameEn} optionalMark ltr />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0" aria-describedby={errors.types ? "types-error" : undefined}>
          <legend className="mb-1 text-15 font-semibold">النوع</legend>
          <div className="flex flex-wrap gap-4">
            {TYPES.map((t) => (
              <Checkbox key={t} label={ORG_TYPE_LABELS[t]} checked={v.types.includes(t)}
                onChange={(e) => set({ types: e.target.checked ? [...v.types, t] : v.types.filter((x) => x !== t) })} />
            ))}
          </div>
          {errors.types ? <span id="types-error" className="text-13 text-err">{errors.types}</span> : null}
        </fieldset>
        <TextField label="الموقع الرسمي" value={v.website} onChange={(e) => set({ website: e.target.value })} error={errors.website} optionalMark ltr placeholder="https://" />
        <div className="grid gap-4 md:grid-cols-2">
          <TextField label="رقم الترخيص" value={v.licenseNumber} onChange={(e) => set({ licenseNumber: e.target.value })} error={errors.licenseNumber} optionalMark ltr
            help="فقط إن نشره مصدر رسمي (اذكره أدناه)." />
          <TextField label="رقم السجل / الرقم الموحد" value={v.registrationNumber} onChange={(e) => set({ registrationNumber: e.target.value })} error={errors.registrationNumber} optionalMark ltr
            help="فقط إن نشره مصدر رسمي (اذكره أدناه)." />
        </div>
        <TextField label="اسم المصدر" value={v.sourceName} onChange={(e) => set({ sourceName: e.target.value })} error={errors.sourceName} optionalMark
          placeholder="مثال: البنك المركزي السعودي — البنوك المرخصة" />
        <TextField label="رابط المصدر" value={v.sourceUrl} onChange={(e) => set({ sourceUrl: e.target.value })} error={errors.sourceUrl} optionalMark ltr placeholder="https://" />
        <DateField label="تاريخ التحقق" value={v.verifiedOn} onValueChange={(x) => set({ verifiedOn: x })} error={errors.verifiedOn} optionalMark />
        <p className="m-0 text-13 text-muted">وجود الجهة في الدليل لا يعني شراكة مع رهون أو موافقتها على نقل العقود، ولا يُعد إثباتًا لترخيصها ما لم يُذكر المصدر الرسمي.</p>
        <div>
          <Button type="submit" loading={busy}>{record ? "حفظ التعديلات" : "إضافة الجهة"}</Button>
        </div>
      </form>
    </Card>
  );
}

/** Activate or deactivate (never delete). Deactivation needs a reason; requests that named the organization keep it. */
export function DirectoryActiveToggle({ record }: { record: DirectoryRecord }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; fields: Record<string, string>; reasons: string[] } | null>(null);

  const run = async (path: string, body: unknown) => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", path, body);
      router.refresh();
      return true;
    } catch (err) {
      setError(isApiError(err)
        ? { title: err.title || "تعذّر تنفيذ الإجراء.", fields: Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, x]) => [k, x[0]])), reasons: [] }
        : { title: "تعذّر الاتصال. أعد المحاولة.", fields: {}, reasons: [] });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <Badge tone={record.active ? "ok" : "neutral"}>{record.active ? "مفعّلة — تظهر في النماذج" : "موقوفة — لا تظهر في النماذج"}</Badge>
        {record.active ? (
          <Button variant="secondary" onClick={() => setOpen(true)}>إيقاف الجهة</Button>
        ) : (
          <Button variant="secondary" loading={busy} onClick={() => void run(`/team/directory/${record.id}/activate`, { version: record.version })}>تفعيل الجهة</Button>
        )}
      </div>
      {!open ? <ActionError error={error} /> : null}
      <ActionDialog open={open} onClose={() => setOpen(false)} title="إيقاف الجهة" confirm="إيقاف" tone="sensitive" busy={busy}
        onConfirm={() => run(`/team/directory/${record.id}/deactivate`, { reason, version: record.version })}>
        <div className="flex flex-col gap-3">
          <p className="m-0 text-15">لن تظهر في نماذج البيع والشراء. الطلبات التي ذكرتها تحتفظ باسمها ومرجعها كما سُجّلا.</p>
          <Textarea label="السبب" value={reason} onChange={(e) => setReason(e.target.value)} error={error?.fields.reason} requiredMark />
          {error && !error.fields.reason ? <ActionError error={error} /> : null}
        </div>
      </ActionDialog>
    </div>
  );
}
