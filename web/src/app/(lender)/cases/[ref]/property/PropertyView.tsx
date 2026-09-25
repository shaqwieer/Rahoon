"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  Alert,
  Button,
  Checkbox,
  DateText,
  Dialog,
  EmptyState,
  Icon,
  KeyValueList,
  Money,
  RadioCardGroup,
  Select,
  Tag,
  Textarea,
  TextField,
  useToast,
} from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey, type ApiError } from "@/lib/api/client";
import type { PropertyData } from "@/lib/api/caseInfo";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";

type FormError = ApiError | { title: string; fieldError: (f: string) => string | undefined };
const toError = (e: unknown, fallback: string): FormError => (isApiError(e) && e.title ? e : { title: fallback, fieldError: (f) => (isApiError(e) ? e.fieldError(f) : undefined) });

export function PropertyView({ reference, data, isLegal, canViewPhoto }: { reference: string; data: PropertyData; isLegal: boolean; canViewPhoto: boolean }) {
  const [editOpen, setEditOpen] = useState(false);
  const [seq, setSeq] = useState(0);
  const p = data.property;
  const m = data.mortgage;

  return (
    <div className="grid items-start gap-5 lg:grid-cols-2">
      <section aria-labelledby="prop-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
        {p ? (
          <>
            <Photo reference={reference} photo={p.photo} canView={canViewPhoto} />
            <div className="flex items-center gap-2">
              <h2 id="prop-h" className="m-0 flex-1 text-18 font-semibold">العقار</h2>
              {data.canEditProperty ? (
                <Button variant="text" size="sm" icon="edit" onClick={() => { setSeq((n) => n + 1); setEditOpen(true); }}>تعديل بيانات العقار</Button>
              ) : null}
            </div>
            <KeyValueList
              dense
              rows={[
                { key: "النوع", value: p.type },
                { key: "الموقع", value: p.location || "—" },
                { key: "مساحة الأرض", value: p.landAreaM2 === null ? "—" : <><bdi dir="ltr" className="tabular-nums">{formatNumber(p.landAreaM2)}</bdi> م²</> },
                { key: "مساحة البناء", value: p.builtAreaM2 === null ? "—" : <><bdi dir="ltr" className="tabular-nums">{formatNumber(p.builtAreaM2)}</bdi> م²</> },
                { key: "سنة البناء", value: p.yearBuilt === null ? "—" : <bdi dir="ltr">{p.yearBuilt}</bdi> },
                { key: "رقم الصك", value: p.deedMasked ? <bdi dir="ltr" className="font-mono">{p.deedMasked}</bdi> : "—" },
                { key: "الإشغال", value: p.occupancyLabel },
                {
                  key: "القيمة (التقييم)",
                  value:
                    p.valuationValue === null ? (
                      "لا يوجد تقييم معتمد"
                    ) : (
                      <span className="flex flex-col items-end">
                        <Money value={p.valuationValue} strong />
                        {p.valuationSource ? <span className="text-12 text-muted">{p.valuationSource}</span> : null}
                      </span>
                    ),
                },
              ]}
            />
            {p.occupancyNote ? <p className="m-0 text-13 text-muted">{p.occupancyNote}</p> : null}
            {p.protectionNote ? (
              <Alert tone="warn" icon="family_restroom" role="none">
                <strong>{p.protectionNote.title}</strong> {p.protectionNote.body}
              </Alert>
            ) : null}
          </>
        ) : (
          <>
            <h2 id="prop-h" className="m-0 text-18 font-semibold">العقار</h2>
            <EmptyState icon="home" title="لا توجد بيانات عقار" body="لم تُسجَّل بيانات العقار لهذه الحالة بعد." headingLevel={3} />
          </>
        )}
      </section>

      <div className="flex flex-col gap-4">
        <section aria-labelledby="mort-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <div className="flex flex-wrap items-center gap-2">
            <h2 id="mort-h" className="m-0 flex-1 text-18 font-semibold">الرهن والضمانات</h2>
            {m ? <SplitBadge label="مراجعة قانونية" value={m.legalReview.label} tone={m.legalReview.status === "complete" ? "ok" : "warn"} /> : null}
          </div>
          {m ? (
            <KeyValueList
              dense
              rows={[
                { key: "الجهة المرتهنة", value: m.mortgagee },
                { key: "درجة الرهن", value: m.rankLabel },
                { key: "تاريخ التسجيل", value: m.registeredOn ? <DateText value={m.registeredOn} /> : "—" },
                { key: "قيود أخرى", value: m.otherEncumbrances },
                { key: "التأمين على العقار", value: <InsuranceValue label={m.insuranceLabel} tone={m.insuranceTone} /> },
                { key: "مطابقة الصك", value: <span className={m.deedMatched ? "text-ok" : "text-warn"}><bdi dir="auto">{m.deedMatchLabel}</bdi></span> },
                { key: "مصدر التحقق", value: m.verificationSource ?? "—" },
              ]}
            />
          ) : (
            <p className="m-0 text-14 text-muted">لا توجد بيانات رهن مسجلة لهذه الحالة.</p>
          )}
          {data.blocksProposal ? (
            <Alert tone="warn" icon="block" compact title="تحجب الانتقال إلى «حل مقترح»">
              {data.blocksProposal}
            </Alert>
          ) : null}
        </section>

        {m?.legalReview.note ? (
          <section aria-labelledby="legal-h" className="flex flex-col gap-2 rounded-lg border border-line bg-warm p-5">
            <h3 id="legal-h" className="m-0 text-15 font-semibold">ملاحظة القانونية</h3>
            <p className="m-0 text-14 leading-6">{m.legalReview.note}</p>
            {m.legalReview.footer ? <span className="text-12 text-muted"><bdi dir="auto">{m.legalReview.footer}</bdi></span> : null}
          </section>
        ) : null}

        {m && isLegal ? <LegalReviewForm key={m.legalReview.at ?? "none"} reference={reference} mortgage={m} enabled={data.canLegalReview} /> : null}

        {data.inspection ? (
          <section aria-labelledby="insp-h" className="flex items-start gap-3 rounded-lg border border-line bg-white p-5">
            <span className="grid size-10 flex-none place-items-center rounded-md bg-subtle">
              <Icon name="event" size={22} />
            </span>
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <h3 id="insp-h" className="m-0 text-15 font-semibold">معاينة المقيّم</h3>
              <span className="text-14"><bdi dir="auto">{data.inspection.text}</bdi></span>
              {data.inspection.providerAssignment ? (
                <span className="text-12 text-muted">التكليف <bdi dir="ltr" className="font-mono">{data.inspection.providerAssignment}</bdi></span>
              ) : null}
            </div>
            <Tag tone={data.inspection.status === "completed" ? "ok" : data.inspection.status === "scheduled" ? "info" : "warn"}>{data.inspection.statusLabel}</Tag>
          </section>
        ) : null}
      </div>

      {p && data.canEditProperty ? (
        <PropertyDialog key={`prop-${seq}`} reference={reference} property={p} hasMortgage={m !== null} open={editOpen} onClose={() => setEditOpen(false)} />
      ) : null}
    </div>
  );
}

function Photo({ reference, photo, canView }: { reference: string; photo: NonNullable<PropertyData["property"]>["photo"]; canView: boolean }) {
  if (photo?.versionId && canView) {
    return (
      <figure className="m-0 flex flex-col gap-1">
        {/* eslint-disable-next-line @next/next/no-img-element -- authenticated same-origin API stream, not a static asset */}
        <img
          src={`/api/cases/${reference}/documents/versions/${photo.versionId}/file`}
          alt="صورة واجهة العقار من تقرير التقييم"
          className="aspect-[16/9] w-full rounded-md border border-line bg-subtle object-cover"
        />
        {photo.source ? <figcaption className="text-12 text-muted">{photo.source}</figcaption> : null}
      </figure>
    );
  }
  return (
    <div role="img" aria-label="مكان صورة واجهة العقار من تقرير التقييم" className="grid aspect-[16/9] w-full place-items-center rounded-md border border-dashed border-line-strong bg-warm text-13 text-muted">
      <span className="flex flex-col items-center gap-1">
        <Icon name="photo_camera" size={28} />
        {photo ? "الصورة محفوظة في ملف الحالة" : "لا توجد صورة معتمدة من تقرير التقييم"}
      </span>
    </div>
  );
}

function SplitBadge({ label, value, tone }: { label: string; value: string; tone: "ok" | "warn" }) {
  return (
    <span className="inline-flex overflow-hidden rounded-xs border border-line text-12 font-semibold">
      <span className="bg-subtle px-2 py-[3px]">{label}</span>
      <span className={cn("inline-flex items-center gap-1 px-2 py-[3px]", tone === "ok" ? "bg-ok-bg text-ok" : "bg-warn-bg text-warn")}>
        <Icon name={tone === "ok" ? "check_circle" : "pending"} size={14} />
        {value}
      </span>
    </span>
  );
}

function InsuranceValue({ label, tone }: { label: string; tone: string }) {
  const cls = tone === "ok" ? "text-ok" : tone === "warn" ? "text-warn" : tone === "err" ? "text-err" : "";
  return <span className={cls}><bdi dir="auto">{label}</bdi></span>;
}

/* ───────── L08 legal review (legal role only) ───────── */

function LegalReviewForm({ reference, mortgage: m, enabled }: { reference: string; mortgage: NonNullable<PropertyData["mortgage"]>; enabled: boolean }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [status, setStatus] = useState<"complete" | "pending">(m.legalReview.status);
  const [note, setNote] = useState("");
  const [deedMatched, setDeedMatched] = useState(m.deedMatched);
  const [encumbrances, setEncumbrances] = useState(m.otherEncumbrances === "لا توجد" ? "" : m.otherEncumbrances);
  const [source, setSource] = useState(m.verificationSource ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<FormError | null>(null);
  const touch = () => key.reset();

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend(
        "POST",
        `/cases/${reference}/mortgage/legal-review`,
        { status, note: note.trim(), deedMatched, otherEncumbrances: encumbrances.trim(), verificationSource: source.trim() || null },
        { idempotencyKey: key.get() },
      );
      key.reset();
      toast.toast({ tone: "ok", message: status === "complete" ? "سُجّل اكتمال المراجعة القانونية للرهن." : "أُعيدت المراجعة القانونية إلى «معلقة»." });
      router.refresh();
    } catch (e) {
      key.reset();
      setError(toError(e, "تعذّر حفظ المراجعة القانونية."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="lr-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-col gap-1">
        <h3 id="lr-h" className="m-0 text-16 font-semibold">المراجعة القانونية للرهن</h3>
        <p className="m-0 text-13 text-muted">القانونية تعدّل حالة المراجعة؛ الباقون قراءة. تُسجَّل الملاحظة والمراجِع والوقت في السجل.</p>
      </div>
      {!enabled ? (
        <Alert tone="info" role="none" compact>لا يمكن تعديل المراجعة الآن: الحالة مغلقة أو ملغاة، أو لا توجد بيانات رهن.</Alert>
      ) : (
        <>
          {error ? <Alert tone="err" title={error.title} /> : null}
          <RadioCardGroup
            legend="حالة المراجعة"
            name="legal-status"
            columns={2}
            value={status}
            onChange={(v) => { touch(); setStatus(v as "complete" | "pending"); }}
            options={[
              { value: "complete", label: "مكتملة", description: "تسمح بالانتقال إلى «حل مقترح»." },
              { value: "pending", label: "معلقة", description: "تحجب الانتقال إلى «حل مقترح»." },
            ]}
            error={error?.fieldError("status")}
          />
          <Checkbox label="الصك مطابق للمستند المرفوع" checked={deedMatched} onChange={(e) => { touch(); setDeedMatched(e.target.checked); }} />
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="قيود أخرى" optionalMark value={encumbrances} placeholder="لا توجد" maxLength={500} onChange={(e) => { touch(); setEncumbrances(e.target.value); }} error={error?.fieldError("otherEncumbrances")} />
            <TextField label="مصدر التحقق" optionalMark value={source} maxLength={200} placeholder="مستند مرفوع + مراجعة القانونية" onChange={(e) => { touch(); setSource(e.target.value); }} error={error?.fieldError("verificationSource")} />
          </div>
          <Textarea
            label="ملاحظة القانونية"
            requiredMark
            value={note}
            maxLength={2000}
            onChange={(e) => { touch(); setNote(e.target.value); }}
            placeholder="مثال: الرهن مسجل بالدرجة الأولى ولا توجد قيود لاحقة."
            help="10 أحرف على الأقل. تحل محل الملاحظة الحالية وتظهر لفريق الحالة."
            error={error?.fieldError("note")}
          />
          <div className="flex justify-end">
            <Button onClick={() => void submit()} loading={busy} disabled={note.trim().length < 10}>حفظ المراجعة</Button>
          </div>
        </>
      )}
    </section>
  );
}

/* ───────── L09 property edit ───────── */

const OCCUPANCY = [
  { value: "OwnerFamily", label: "يسكنها المالك وأسرته" },
  { value: "Tenant", label: "مؤجّر لمستأجر" },
  { value: "Vacant", label: "شاغر" },
  { value: "Unknown", label: "غير معروف" },
];

function PropertyDialog({ reference, property: p, hasMortgage, open, onClose }: {
  reference: string;
  property: NonNullable<PropertyData["property"]>;
  hasMortgage: boolean;
  open: boolean;
  onClose: () => void;
}) {
  const router = useRouter();
  const toast = useToast();
  const initial = {
    type: p.type,
    city: p.city,
    district: p.district ?? "",
    landAreaM2: p.landAreaM2?.toString() ?? "",
    builtAreaM2: p.builtAreaM2?.toString() ?? "",
    yearBuilt: p.yearBuilt?.toString() ?? "",
    deedNumber: "",
    occupancy: p.occupancy,
    occupancyNote: p.occupancyNote ?? "",
  };
  const [f, setF] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<FormError | null>(null);
  const set = (k: keyof typeof initial) => (v: string) => setF((prev) => ({ ...prev, [k]: v }));

  const save = async () => {
    const body: Record<string, unknown> = {};
    for (const k of ["type", "city", "district", "occupancy", "occupancyNote"] as const) if (f[k].trim() !== initial[k]) body[k] = f[k].trim();
    for (const k of ["landAreaM2", "builtAreaM2", "yearBuilt"] as const) if (f[k].trim() !== initial[k] && f[k].trim() !== "") body[k] = Number(f[k]);
    if (f.deedNumber.trim()) body.deedNumber = f.deedNumber.trim();
    if (Object.keys(body).length === 0) {
      onClose();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ changed: string[] }>("PATCH", `/cases/${reference}/property`, body);
      toast.toast({ tone: "ok", message: res.changed.length ? `حُفظ التعديل: ${res.changed.join("، ")}` : "لا تغييرات." });
      onClose();
      router.refresh();
    } catch (e) {
      setError(toError(e, "تعذّر حفظ بيانات العقار."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      title="تعديل بيانات العقار"
      description="يُسجَّل التعديل بأسماء الحقول في سجل الحالة."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void save()} loading={busy}>حفظ التعديل</Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 p-5 sm:grid-cols-2">
        {error ? <Alert tone="err" title={error.title} className="sm:col-span-2" /> : null}
        <TextField label="النوع" value={f.type} onChange={(e) => set("type")(e.target.value)} maxLength={100} error={error?.fieldError("type")} containerClassName="sm:col-span-2" />
        <TextField label="المدينة" value={f.city} onChange={(e) => set("city")(e.target.value)} maxLength={60} error={error?.fieldError("city")} />
        <TextField label="الحي" optionalMark value={f.district} onChange={(e) => set("district")(e.target.value)} maxLength={80} error={error?.fieldError("district")} />
        <TextField label="مساحة الأرض (م²)" ltr inputMode="decimal" value={f.landAreaM2} onChange={(e) => set("landAreaM2")(e.target.value)} error={error?.fieldError("landAreaM2")} />
        <TextField label="مساحة البناء (م²)" ltr inputMode="decimal" value={f.builtAreaM2} onChange={(e) => set("builtAreaM2")(e.target.value)} error={error?.fieldError("builtAreaM2")} />
        <TextField label="سنة البناء" ltr inputMode="numeric" maxLength={4} value={f.yearBuilt} onChange={(e) => set("yearBuilt")(e.target.value)} error={error?.fieldError("yearBuilt")} />
        <Select label="الإشغال" value={f.occupancy} onChange={(e) => set("occupancy")(e.target.value)} options={OCCUPANCY} error={error?.fieldError("occupancy")} />
        <Textarea label="ملاحظة الإشغال" optionalMark value={f.occupancyNote} onChange={(e) => set("occupancyNote")(e.target.value)} maxLength={500} error={error?.fieldError("occupancyNote")} containerClassName="sm:col-span-2" />
        <TextField
          label="رقم الصك الجديد"
          optionalMark
          ltr
          mono
          inputMode="numeric"
          maxLength={14}
          value={f.deedNumber}
          onChange={(e) => set("deedNumber")(e.target.value)}
          placeholder={p.deedMasked ?? undefined}
          help="اتركه فارغاً للإبقاء على الصك الحالي."
          error={error?.fieldError("deedNumber")}
          containerClassName="sm:col-span-2"
        />
        {f.deedNumber.trim() && hasMortgage ? (
          <Alert tone="warn" compact className="sm:col-span-2" title="تغيير الصك يعيد المراجعة القانونية">
            تُلغى مطابقة الصك وتعود المراجعة القانونية إلى «معلقة»، فتحجب الانتقال إلى «حل مقترح» حتى تكتمل.
          </Alert>
        ) : null}
      </div>
    </Dialog>
  );
}
