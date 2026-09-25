"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, AmountField, Button, Checkbox, DateText, Dialog, Icon, IconButton, Select, Tag, TextField, useToast } from "@/components/ui";
import { apiSend, isApiError } from "@/lib/api/client";
import type { AnalysisData, AnalysisIndicator, AnalysisOption } from "@/lib/api/valuation";
import { cn } from "@/lib/cn";
import { formatPercent } from "@/lib/format";

export interface IncomeDocOption {
  versionId: string;
  label: string;
}

const pct = (r: number | null | undefined) => (r === null || r === undefined ? "—" : formatPercent(r * 100, { fractionDigits: 1 }));

/** L12 — affordability (verified income, obligations, DSR vs policy limit), circumstance indicators, possible options; edit with optimistic concurrency. */
export function AnalysisView({ data, reference, incomeDocs }: { data: AnalysisData; reference: string; incomeDocs: IncomeDocOption[] }) {
  const [editing, setEditing] = useState(false);
  const a = data.affordability;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {data.completed ? <Tag tone="ok" icon="task_alt">التحليل مكتمل</Tag> : <Tag tone="warn" icon="pending">التحليل غير مكتمل</Tag>}
        {data.preparedBy ? <span className="text-13 text-muted">أعدّه {data.preparedBy}</span> : null}
        {data.canEdit ? (
          <Button variant="secondary" icon="edit" className="ms-auto" onClick={() => setEditing(true)}>
            {data.exists ? "تعديل التحليل" : "بدء التحليل"}
          </Button>
        ) : null}
      </div>
      {data.missingForCompletion.length > 0 ? (
        <Alert tone="info" role="none" title="لإكمال التحليل ينقص:">
          {data.missingForCompletion.join(" · ")}
        </Alert>
      ) : null}

      <div className="grid items-start gap-4 lg:grid-cols-3">
        <section aria-labelledby="aff-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h3 id="aff-h" className="m-0 text-17 font-semibold">القدرة على السداد</h3>
          <dl className="m-0 flex flex-col gap-2 text-14">
            {a.rows.map((r) => (
              <div key={r.k} className="flex items-baseline justify-between gap-3">
                <dt className={cn("text-muted", r.strong && "font-semibold text-ink")}>{r.k}</dt>
                <dd className={cn("m-0 text-end", r.strong && "font-bold")}><bdi dir="ltr" className="tabular-nums">{r.v}</bdi></dd>
              </div>
            ))}
          </dl>
          <DsrMeter current={a.currentDsr} proposed={a.proposed?.dsr ?? null} proposedVersion={a.proposed?.version ?? null} limit={a.dsrLimit} />
          {a.incomeSource ? (
            <p className={cn("m-0 flex items-start gap-1.5 text-13", a.incomeSource.verified ? "text-ok" : "text-warn")}>
              <Icon name={a.incomeSource.verified ? "verified" : "pending"} size={16} />
              <span>
                مصدر الدخل: {a.incomeSource.label}
                {a.incomeSource.verified && a.incomeSource.verifiedOn ? <> · متحقق <DateText value={a.incomeSource.verifiedOn} /></> : a.incomeSource.verified ? " · متحقق" : " · غير متحقق"}
              </span>
            </p>
          ) : (
            <p className="m-0 text-13 text-muted">لا يوجد مصدر دخل متحقق بعد.</p>
          )}
          {a.dsrIncludingObligations.current !== null ? (
            <p className="m-0 text-13 text-muted">
              مع الالتزامات الأخرى: الحالي <bdi dir="ltr">{pct(a.dsrIncludingObligations.current)}</bdi>
              {a.dsrIncludingObligations.proposed !== null ? <> · المقترح <bdi dir="ltr">{pct(a.dsrIncludingObligations.proposed)}</bdi></> : null}
            </p>
          ) : null}
          <p className="m-0 text-12 text-muted">{a.formula}</p>
        </section>

        <section aria-labelledby="ind-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h3 id="ind-h" className="m-0 text-17 font-semibold">مؤشرات الظرف</h3>
          {data.circumstanceIndicators.length === 0 ? (
            <p className="m-0 text-14 text-muted">لم تُدخل مؤشرات بعد.</p>
          ) : (
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0 text-14">
              {data.circumstanceIndicators.map((i, n) => (
                <li key={n} className="flex items-start gap-2">
                  <Icon name={i.icon ?? "info"} size={20} className="text-charcoal" />
                  <span>{i.text}</span>
                </li>
              ))}
            </ul>
          )}
          <p className="m-0 text-12 text-muted">{data.indicatorsCaption}</p>
        </section>

        <section aria-labelledby="opt-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h3 id="opt-h" className="m-0 text-17 font-semibold">الخيارات الممكنة</h3>
          {data.options.length === 0 ? (
            <p className="m-0 text-14 text-muted">لم تُدخل خيارات بعد.</p>
          ) : (
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0 text-14">
              {data.options.map((o, n) => (
                <li key={n} className="flex items-start gap-2">
                  <Icon name={o.feasible ? "check_circle" : "remove"} size={20} className={o.feasible ? "text-ok" : "text-muted"} />
                  <span>
                    <span className="sr-only">{o.feasible ? "ممكن: " : "غير متاح: "}</span>
                    <strong>{o.title}</strong>
                    {o.note ? <> — {o.note}</> : null}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <p className="m-0 text-12 text-muted">{data.optionsCaption}</p>
        </section>
      </div>

      {data.canEdit ? <EditAnalysisDialog open={editing} onClose={() => setEditing(false)} data={data} reference={reference} incomeDocs={incomeDocs} /> : null}
    </div>
  );
}

/** DSR meter: bars against the policy limit tick, with the numbers as text (never colour only). */
function DsrMeter({ current, proposed, proposedVersion, limit }: { current: number | null; proposed: number | null; proposedVersion: number | null; limit: number }) {
  if (current === null && proposed === null) return null;
  const scale = Math.max(limit * 1.4, current ?? 0, proposed ?? 0, 0.01);
  const rows: Array<{ label: string; value: number | null }> = [
    { label: "الحالي", value: current },
    ...(proposed !== null ? [{ label: proposedVersion ? `المقترح v${proposedVersion}` : "المقترح", value: proposed }] : []),
  ];
  const limitPos = `${(limit / scale) * 100}%`;
  return (
    <figure className="m-0 flex flex-col gap-2 rounded-md bg-warm p-3" aria-label="نسبة الاستقطاع مقابل حد السياسة">
      {rows.map((r) => {
        const within = r.value !== null && r.value <= limit;
        return (
          <div key={r.label} className="flex flex-col gap-1">
            <div className="flex items-center justify-between text-13">
              <span>{r.label}</span>
              <span className={cn("font-semibold", within ? "text-ok" : "text-err")}>
                <bdi dir="ltr" className="tabular-nums">{pct(r.value)}</bdi> {within ? "ضمن الحد" : "فوق الحد"}
              </span>
            </div>
            <div aria-hidden="true" className="relative h-2 rounded-pill bg-track">
              <span className={cn("absolute inset-y-0 start-0 rounded-pill", within ? "bg-ok" : "bg-err")} style={{ width: `${Math.min(100, ((r.value ?? 0) / scale) * 100)}%` }} />
              <span className="absolute -inset-y-1 w-0.5 bg-ink" style={{ insetInlineStart: limitPos }} />
            </div>
          </div>
        );
      })}
      <figcaption className="text-12 text-muted">الخط الداكن = حد السياسة <bdi dir="ltr">{pct(limit)}</bdi> (افتراض)</figcaption>
    </figure>
  );
}

const INDICATOR_ICONS = [
  { value: "work_history", label: "العمل والدخل" },
  { value: "home", label: "السكن والأسرة" },
  { value: "handshake", label: "التعاون والتواصل" },
  { value: "medical_services", label: "الصحة" },
  { value: "family_restroom", label: "الأسرة" },
  { value: "info", label: "أخرى" },
];

const OPTION_KINDS = [
  { value: "reschedule", label: "إعادة جدولة" },
  { value: "waiver", label: "تنازل عن الغرامات" },
  { value: "reduced_payoff", label: "سداد مخفض" },
  { value: "grace_period", label: "فترة سماح" },
  { value: "voluntary_sale", label: "بيع طوعي" },
  { value: "other", label: "أخرى" },
];

function EditAnalysisDialog({ open, onClose, data, reference, incomeDocs }: {
  open: boolean;
  onClose: () => void;
  data: AnalysisData;
  reference: string;
  incomeDocs: IncomeDocOption[];
}) {
  const router = useRouter();
  const toast = useToast();
  const a = data.affordability;
  const origDoc = a.incomeSource?.documentVersionId ?? "";
  const [income, setIncome] = useState<number | null>(a.netMonthlyIncome);
  const [docId, setDocId] = useState(origDoc);
  const [obligations, setObligations] = useState<number | null>(a.otherObligations);
  const [indicators, setIndicators] = useState<AnalysisIndicator[]>(data.circumstanceIndicators);
  const [options, setOptions] = useState<AnalysisOption[]>(data.options);
  const [completed, setCompleted] = useState(data.completed);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; fields: Record<string, string | undefined> } | null>(null);
  const [conflict, setConflict] = useState(false);

  const docOptions = [
    ...incomeDocs.map((d) => ({ value: d.versionId, label: d.label })),
    ...(origDoc && !incomeDocs.some((d) => d.versionId === origDoc) ? [{ value: origDoc, label: a.incomeSource!.label }] : []),
  ];
  const incomeChanged = income !== a.netMonthlyIncome || docId !== origDoc;

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("PUT", `/cases/${reference}/analysis`, {
        version: data.version,
        ...(incomeChanged ? { netMonthlyIncome: income, incomeDocumentVersionId: docId || null } : {}),
        otherObligations: obligations ?? 0,
        circumstanceIndicators: indicators.filter((i) => i.text.trim()).map((i) => ({ icon: i.icon, text: i.text.trim() })),
        options: options.filter((o) => o.title.trim()).map((o) => ({ kind: o.kind, title: o.title.trim(), feasible: o.feasible ?? false, note: o.note?.trim() || null })),
        completed,
      });
      toast.toast({ tone: "ok", message: "حُفظ التحليل وسُجّل في سجل الحالة." });
      onClose();
      router.refresh();
    } catch (e) {
      if (isApiError(e) && e.code === "concurrency") setConflict(true);
      else if (isApiError(e)) {
        setError({
          title: e.title || "تعذّر حفظ التحليل.",
          fields: {
            netMonthlyIncome: e.fieldError("netMonthlyIncome"),
            incomeDocumentVersionId: e.fieldError("incomeDocumentVersionId"),
            otherObligations: e.fieldError("otherObligations"),
            circumstanceIndicators: e.fieldError("circumstanceIndicators"),
            options: e.fieldError("options"),
            completed: e.fieldError("completed"),
          },
        });
      } else setError({ title: "تعذّر حفظ التحليل.", fields: {} });
    } finally {
      setBusy(false);
    }
  };

  const patchIndicator = (n: number, p: Partial<AnalysisIndicator>) => setIndicators((xs) => xs.map((x, i) => (i === n ? { ...x, ...p } : x)));
  const patchOption = (n: number, p: Partial<AnalysisOption>) => setOptions((xs) => xs.map((x, i) => (i === n ? { ...x, ...p } : x)));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      title="تعديل تحليل القدرة على السداد"
      description="الأرقام تُعتمد من مستندات متحقق منها فقط. حد الاستقطاع إعداد للمنشأة ولا يُعدّل هنا."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void save()} loading={busy} disabled={conflict || (incomeChanged && income !== null && !docId)}>حفظ التحليل</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {conflict ? (
          <Alert tone="warn" title="تغيّر التحليل منذ فتحه" action={<Button variant="secondary" size="sm" onClick={() => { onClose(); router.refresh(); }}>إعادة التحميل</Button>}>
            عدّل مستخدم آخر هذا التحليل. أعد التحميل لرؤية أحدث نسخة ثم أعد تعديلاتك.
          </Alert>
        ) : null}
        {error ? <Alert tone="err" title={error.title} /> : null}

        <div className="grid gap-4 md:grid-cols-2">
          <AmountField label="صافي الدخل الشهري المتحقق" value={income} onValueChange={setIncome} error={error?.fields.netMonthlyIncome} />
          <Select
            label="مستند الدخل (إصدار متحقق)"
            requiredMark={incomeChanged && income !== null}
            placeholder={docOptions.length ? "اختر الإصدار" : "لا يوجد مستند دخل متحقق"}
            value={docId}
            onChange={(e) => setDocId(e.target.value)}
            options={docOptions}
            error={error?.fields.incomeDocumentVersionId}
            help="كشف راتب أو كشف حساب أو تعريف بالراتب متحقق منه على هذه الحالة."
          />
          <AmountField label="التزامات أخرى" value={obligations} onValueChange={setObligations} error={error?.fields.otherObligations} help="تُعرض بجانب نسبة الاستقطاع ولا تدخل فيها." />
        </div>

        <fieldset className="m-0 flex flex-col gap-3 border-0 p-0">
          <legend className="mb-1 text-15 font-semibold">مؤشرات الظرف</legend>
          <p className="m-0 text-13 text-muted">وصفية لا تصنيفية · تُستخدم لاختيار الحل، لا لتقييم الشخص.</p>
          {indicators.map((i, n) => (
            <div key={n} className="grid items-end gap-2 sm:grid-cols-[160px_minmax(0,1fr)_auto]">
              <Select label={`أيقونة المؤشر ${n + 1}`} value={i.icon ?? "info"} onChange={(e) => patchIndicator(n, { icon: e.target.value })} options={INDICATOR_ICONS} />
              <TextField label={`نص المؤشر ${n + 1}`} value={i.text} maxLength={200} onChange={(e) => patchIndicator(n, { text: e.target.value })} />
              <IconButton icon="delete" label={`حذف المؤشر ${n + 1}`} onClick={() => setIndicators((xs) => xs.filter((_, k) => k !== n))} />
            </div>
          ))}
          {error?.fields.circumstanceIndicators ? <span className="text-13 text-err">{error.fields.circumstanceIndicators}</span> : null}
          {indicators.length < 10 ? (
            <Button variant="text" icon="add" className="self-start" onClick={() => setIndicators((xs) => [...xs, { icon: "info", text: "" }])}>إضافة مؤشر</Button>
          ) : null}
        </fieldset>

        <fieldset className="m-0 flex flex-col gap-3 border-0 p-0">
          <legend className="mb-1 text-15 font-semibold">الخيارات الممكنة</legend>
          <p className="m-0 text-13 text-muted">قائمة مرجعية للمحلل؛ القرار بشري ويمر بالموافقات.</p>
          {options.map((o, n) => (
            <div key={n} className="flex flex-col gap-2 rounded-md border border-line p-3">
              <div className="grid items-end gap-2 sm:grid-cols-[180px_minmax(0,1fr)_auto]">
                <Select label={`نوع الخيار ${n + 1}`} value={o.kind ?? "other"} onChange={(e) => patchOption(n, { kind: e.target.value })} options={OPTION_KINDS} />
                <TextField label={`عنوان الخيار ${n + 1}`} value={o.title} maxLength={100} onChange={(e) => patchOption(n, { title: e.target.value })} />
                <IconButton icon="delete" label={`حذف الخيار ${n + 1}`} onClick={() => setOptions((xs) => xs.filter((_, k) => k !== n))} />
              </div>
              <TextField label="الوصف" optionalMark value={o.note ?? ""} maxLength={300} onChange={(e) => patchOption(n, { note: e.target.value })} />
              <Checkbox label="ممكن" checked={o.feasible ?? false} onChange={(e) => patchOption(n, { feasible: e.target.checked })} />
            </div>
          ))}
          {error?.fields.options ? <span className="text-13 text-err">{error.fields.options}</span> : null}
          {options.length < 10 ? (
            <Button variant="text" icon="add" className="self-start"
              onClick={() => setOptions((xs) => [...xs, { kind: "other", title: "", feasible: false, note: null }])}>إضافة خيار</Button>
          ) : null}
        </fieldset>

        <Checkbox
          label="التحليل مكتمل"
          description="يتطلب دخلاً متحققاً من مستند."
          checked={completed}
          onChange={(e) => setCompleted(e.target.checked)}
          error={error?.fields.completed}
        />
      </div>
    </Dialog>
  );
}
