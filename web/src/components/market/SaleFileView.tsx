"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { DocumentManager } from "@/components/market/DocumentManager";
import { Chips, DynamicField } from "@/components/market/DynamicField";
import { MapPicker } from "@/components/market/MapPicker";
import { PhotoManager } from "@/components/market/PhotoManager";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Badge, Card, DemoBadge, SaveState, StatusBadge, Timeline } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Dialog } from "@/components/ui/Dialog";
import { Textarea } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { cityLabel, label, obligationFields, propertyFields, prune, UNKNOWN } from "@/lib/market/catalog";
import { day } from "@/lib/market/format";
import type { Answers, Catalog, SaleFile, SaveResponse } from "@/lib/market/types";

type Group = "property" | "figures" | "media" | "review";

function ReadOnly({ fields, answers, propertyType }: { fields: ReturnType<typeof propertyFields>; answers: Answers; propertyType?: string | null }) {
  const shown = fields.filter((f) => answers[f.key]);
  if (!shown.length) return <p className="m-0 text-14 text-muted">لا توجد بيانات.</p>;
  return (
    <dl className="m-0 grid gap-x-6 gap-y-2 sm:grid-cols-2">
      {shown.map((f) => {
        const v = answers[f.key];
        const text =
          v === UNKNOWN ? "لا أعرف" : f.type === "boolean" ? (v === "true" ? "نعم" : "لا")
          : f.type === "select" ? label((f.options ?? []).filter((o) => !o.propertyTypes || (propertyType && o.propertyTypes.includes(propertyType))), v)
          : f.type === "multiSelect" ? v.split(",").map((x) => label(f.options ?? [], x)).join("، ")
          : f.type === "money" ? `${Number(v).toLocaleString("en-US")} ر.س` : `${v}${f.unit ? ` ${f.unit}` : ""}`;
        return (
          <div key={f.key} className="flex flex-col">
            <dt className="text-13 text-muted">{f.label}</dt>
            <dd className="m-0 text-15 font-semibold">{text}</dd>
          </div>
        );
      })}
    </dl>
  );
}

/**
 * The follow-up file of one sale request (never a second request): property and location, figures and obligations,
 * photos and documents, review and sending. Saved automatically while editable; what is missing is listed per group.
 */
export function SaleFileView({ initial, catalog }: { initial: SaleFile; catalog: Catalog }) {
  const [file, setFile] = useState(initial);
  const [answers, setAnswers] = useState<Answers>(initial.answers);
  const [obligations, setObligations] = useState(initial.obligations);
  const [district, setDistrict] = useState(initial.district ?? "");
  const [project, setProject] = useState(initial.project ?? "");
  const [location, setLocation] = useState(initial.location);
  const [open, setOpen] = useState<Group>(() => (initial.completeness.find((g) => !g.done)?.key as Group) ?? "property");
  const [save, setSave] = useState<"idle" | "saving" | "saved" | "failed">("idle");
  const [invalid, setInvalid] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "err"; text: string } | null>(null);
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [withdrawReason, setWithdrawReason] = useState("");
  const [note, setNote] = useState("");
  const dirty = useRef(0);
  const inflight = useRef(false);
  const editable = file.editable;

  const persist = useCallback(async () => {
    if (inflight.current || dirty.current === 0) return;
    inflight.current = true;
    const stamp = dirty.current;
    setSave("saving");
    try {
      const res = await apiSend<SaveResponse>("PUT", `/market/sale-requests/${file.reference}`, {
        district, project, answers,
        obligations: obligations.map((o) => ({ id: o.id, kind: o.kind, partyId: o.partyId, partyOtherName: o.partyOtherName, relationNote: o.relationNote, answers: o.answers })),
        location: location.lat !== null && location.lng !== null ? { lat: location.lat, lng: location.lng, label: location.label, displayWish: location.displayWish } : undefined,
      });
      setInvalid(res.invalid);
      setFile(res.file);
      if (dirty.current === stamp) dirty.current = 0;
      setSave("saved");
    } catch (err) {
      setSave("failed");
      if (isApiError(err) && err.code === "locked") setMessage({ tone: "err", text: err.title });
    } finally {
      inflight.current = false;
    }
  }, [file.reference, district, project, answers, obligations, location]);

  useEffect(() => {
    if (dirty.current === 0) return;
    const t = window.setTimeout(() => void persist(), 900);
    return () => window.clearTimeout(t);
  }, [persist]);

  // Retry a failed save when the connection comes back.
  useEffect(() => {
    const on = () => void persist();
    window.addEventListener("online", on);
    return () => window.removeEventListener("online", on);
  }, [persist]);

  const touch = () => {
    dirty.current += 1;
  };

  const setAnswer = (key: string, v: string | undefined) => {
    touch();
    setAnswers((a) => {
      const next = { ...a };
      if (v === undefined) delete next[key];
      else next[key] = v;
      return prune(catalog, "property", next, file.propertyType, null);
    });
  };

  const setObAnswer = (id: string, key: string, v: string | undefined) => {
    touch();
    setObligations((obs) =>
      obs.map((o) => {
        if (o.id !== id) return o;
        const next = { ...o.answers };
        if (v === undefined) delete next[key];
        else next[key] = v;
        return { ...o, answers: prune(catalog, "obligation", next, null, o.kind) };
      }),
    );
  };

  const act = async (key: string, path: string, body?: unknown, ok?: string) => {
    setBusy(key);
    setMessage(null);
    try {
      if (dirty.current) await persist();
      await apiSend("POST", `/market/sale-requests/${file.reference}/${path}`, body ?? {});
      if (ok) setMessage({ tone: "ok", text: ok });
      // Refetch in place (a router refresh would remount the file and drop the confirmation message).
      const fresh = await fetch(`/api/market/sale-requests/${file.reference}`, { credentials: "same-origin" }).then((r) => r.json() as Promise<SaleFile>);
      setFile(fresh);
    } catch (err) {
      setMessage({ tone: "err", text: isApiError(err) && err.title ? err.title : "تعذّر تنفيذ الإجراء. حاول مرة أخرى." });
    } finally {
      setBusy(null);
    }
  };

  const city = catalog.cities.find((c) => c.key === file.city);
  const groups = file.completeness;
  const groupDone = (k: Group) => groups.find((g) => g.key === k)?.done ?? false;
  const missing = (k: Group) => groups.find((g) => g.key === k)?.missing ?? [];
  const docDefs = catalog.documents.filter((d) => d.obligationKinds.length === 0 || d.obligationKinds.some((k) => obligations.some((o) => o.kind === k)));

  const section = (key: Group, title: string, icon: string, body: React.ReactNode) => (
    <section className="overflow-hidden rounded-lg border border-line bg-white shadow-1">
      <h2 className="m-0">
        <button type="button" aria-expanded={open === key} onClick={() => setOpen(open === key ? ("" as Group) : key)}
          className="flex min-h-14 w-full items-center gap-3 px-4 text-start md:px-6">
          <span className={cn("flex size-9 flex-none items-center justify-center rounded-full", groupDone(key) ? "bg-ok-bg text-ok" : "bg-subtle text-muted")}>
            <Icon name={groupDone(key) ? "check" : icon} size={20} />
          </span>
          <span className="flex flex-1 flex-col">
            <span className="text-17 font-bold">{title}</span>
            <span className="text-13 font-normal text-muted">{groupDone(key) ? "مكتمل" : `ينقصه ${missing(key).length}`}</span>
          </span>
          <Icon name="expand_more" size={24} className={cn("transition-transform duration-200", open === key && "rotate-180")} />
        </button>
      </h2>
      {open === key ? (
        <div className="flex flex-col gap-5 border-t border-divider px-4 py-5 md:px-6">
          {missing(key).length ? (
            <div className="flex flex-wrap gap-1.5">
              <span className="text-13 text-muted">ينقص:</span>
              {missing(key).map((m) => <Badge key={m.key} tone="warn">{m.label}</Badge>)}
            </div>
          ) : null}
          {body}
        </div>
      ) : null}
    </section>
  );

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Link href="/account" className="text-14">حسابي</Link>
          <Icon name="chevron_left" size={18} mirror className="text-muted" />
          <bdi dir="ltr" className="font-mono text-14">{file.reference}</bdi>
          {file.isDemo ? <DemoBadge /> : null}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="m-0 text-26 leading-9 font-bold">
            {label(catalog.propertyTypes, file.propertyType)} · {cityLabel(catalog, file.city)}
            {file.district ? `، ${file.district}` : ""}
          </h1>
          <div className="flex items-center gap-3">
            {editable ? <SaveState state={save} /> : null}
            <StatusBadge status={file.status} label={file.statusLabel} />
          </div>
        </div>
        <p className="m-0 flex gap-2 rounded-md border border-info-line bg-info-bg p-3 text-15 leading-7">
          <Icon name="flag" size={20} className="mt-1 flex-none text-info" />
          <span><strong>الخطوة التالية: </strong>{file.nextStep}</span>
        </p>
        {message ? <Alert tone={message.tone} role="status">{message.text}</Alert> : null}
        {file.openCompletion ? (
          <Alert tone="warn" title="طلب الفريق استكمال ما يلي">
            <p className="m-0 mb-2">{file.openCompletion.note}</p>
            <ul className="m-0 ps-5">{file.openCompletion.items.map((i) => <li key={i.key}>{i.label}</li>)}</ul>
            <span className="mt-2 block text-12 text-muted">{file.openCompletion.requestedByLabel} · {day(file.openCompletion.requestedAt)}</span>
          </Alert>
        ) : null}
        {file.opportunity?.awaitingYou ? (
          <div className="flex flex-col items-start gap-3 rounded-lg border border-rust-200 bg-rust-50 p-4 md:flex-row md:items-center md:justify-between">
            <span className="flex gap-2 text-15 font-semibold">
              <Icon name="pending_actions" size={22} className="text-rust" />
              جهّز الفريق ملخص فرصتك وينتظر تأكيدك قبل النشر.
            </span>
            <Link href={`/account/sell/${file.reference}/opportunity`} className={buttonClasses({ variant: "primary", size: "md" })}>مراجعة الملخص</Link>
          </div>
        ) : file.opportunity ? (
          <Link href={`/account/sell/${file.reference}/opportunity`} className="text-15 font-semibold">الفرصة {file.opportunity.reference}: {file.opportunity.statusLabel}</Link>
        ) : null}
        {!editable && file.status !== "withdrawn" && file.status !== "rejected" ? (
          <p className="m-0 text-13 text-muted">البيانات مقفلة بعد اعتماد الطلب. لأي تصحيح تواصل مع فريق رهون؛ يمكنك إضافة الصور والمستندات.</p>
        ) : null}
      </header>

      {section("property", "تفاصيل العقار والموقع", "home", (
        <>
          {editable ? (
            <>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="flex flex-col gap-1.5">
                  <label htmlFor="sf-district" className="text-15 font-semibold">الحي</label>
                  <input id="sf-district" list="sf-districts" value={district} onChange={(e) => { touch(); setDistrict(e.target.value); }}
                    className="min-h-12 rounded-sm border border-line-strong bg-white px-3 text-16" />
                  <datalist id="sf-districts">{city?.districts.map((x) => <option key={x} value={x} />)}</datalist>
                </div>
                <div className="flex flex-col gap-1.5">
                  <label htmlFor="sf-project" className="text-15 font-semibold">المشروع (عند انطباقه)</label>
                  <input id="sf-project" value={project} onChange={(e) => { touch(); setProject(e.target.value); }} className="min-h-12 rounded-sm border border-line-strong bg-white px-3 text-16" />
                </div>
              </div>
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                {propertyFields(catalog, file.propertyType, answers).map((f) => (
                  <div key={f.key} className={f.type === "longText" || f.type === "multiSelect" || f.type === "select" ? "md:col-span-2" : undefined}>
                    <DynamicField def={f} value={answers[f.key]} propertyType={file.propertyType} error={invalid[f.key]} idPrefix="p" onChange={(v) => setAnswer(f.key, v)} />
                  </div>
                ))}
              </div>
            </>
          ) : (
            <ReadOnly fields={propertyFields(catalog, file.propertyType, answers)} answers={answers} propertyType={file.propertyType} />
          )}
          <div className="flex flex-col gap-3">
            <h3 className="m-0 text-16 font-bold">الموقع على الخريطة</h3>
            <MapPicker disabled={!editable} value={location.lat !== null && location.lng !== null ? { lat: location.lat, lng: location.lng } : null}
              center={city ? { lat: city.lat, lng: city.lng } : { lat: 24.7136, lng: 46.6753 }}
              onChange={(p, lbl) => { touch(); setLocation((l) => ({ ...l, lat: p.lat, lng: p.lng, label: lbl ?? l.label, displayWish: l.displayWish ?? "approximate" })); }} />
            {invalid.location ? <span className="text-13 text-err">{invalid.location}</span> : null}
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-15 font-semibold">ما يظهر للمشترين</legend>
              <Chips name="displayWish" options={[{ value: "approximate", label: "منطقة تقريبية فقط" }, { value: "exact", label: "الموقع الدقيق" }]} value={location.displayWish ?? "approximate"}
                onChange={(v) => { if (!editable) return; touch(); setLocation((l) => ({ ...l, displayWish: v })); }} />
              <span className="text-13 text-muted">نحفظ الموقع الحقيقي للفريق. يقرر الفريق دقة العرض العام بموافقتك، ولا نعرض الموقع الدقيق إذا اخترت المنطقة التقريبية.</span>
            </fieldset>
          </div>
        </>
      ))}

      {section("figures", "الأرقام والالتزامات", "payments", (
        <div className="flex flex-col gap-6">
          {obligations.map((o, i) => (
            <div key={o.id} className="flex flex-col gap-4">
              <h3 className="m-0 flex flex-wrap items-center gap-2 text-16 font-bold">
                {o.kind === "developer" ? "الالتزام لدى المطور" : "التمويل لدى البنك أو الجهة"}
                <Badge>{o.partyName || o.partyOtherName}</Badge>
              </h3>
              {o.verified.length ? (
                <div className="flex flex-wrap gap-1.5">
                  {o.verified.map((v) => (
                    <Badge key={v.key} tone="ok" icon="verified">
                      راجع الفريق: {catalog.fields.find((f) => f.key === v.key)?.label}
                    </Badge>
                  ))}
                </div>
              ) : null}
              {editable ? (
                <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                  {obligationFields(catalog, o.kind, o.answers).map((f) => (
                    <div key={f.key} className={f.type === "select" ? "md:col-span-2" : undefined}>
                      <DynamicField def={f} value={o.answers[f.key]} error={invalid[`o${i}.${f.key}`]} idPrefix={`o${i}`} onChange={(v) => setObAnswer(o.id, f.key, v)} />
                    </div>
                  ))}
                </div>
              ) : (
                <ReadOnly fields={obligationFields(catalog, o.kind, o.answers)} answers={o.answers} />
              )}
            </div>
          ))}
        </div>
      ))}

      {section("media", "الصور والمستندات", "photo_library", (
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-3">
            <h3 className="m-0 text-16 font-bold">صور العقار للعرض</h3>
            <PhotoManager reference={file.reference} photos={file.photos} editable={file.filesEditable} />
          </div>
          <div className="flex flex-col gap-3">
            <h3 className="m-0 text-16 font-bold">المستندات الخاصة</h3>
            <DocumentManager reference={file.reference} docs={file.documents} defs={docDefs} editable={file.filesEditable} />
          </div>
        </div>
      ))}

      {section("review", "المراجعة والإرسال للفريق", "send", (
        <div className="flex flex-col gap-5">
          <div className="flex flex-col gap-2">
            <h3 className="m-0 text-16 font-bold">نتيجة أولية تقديرية</h3>
            <TermsBreakdown result={file.estimate} audience="owner" />
          </div>
          {file.status === "needsCompletion" ? (
            <div className="flex flex-col gap-3 rounded-md border border-line p-4">
              <Textarea label="ملاحظة للفريق (اختياري)" value={note} onChange={(e) => setNote(e.target.value)} rows={3} maxLength={1000} />
              <Button onClick={() => void act("resubmit", "resubmit", { note }, "أرسلت الملف للمراجعة.")} loading={busy === "resubmit"} className="self-start">
                إرسال الملف للمراجعة
              </Button>
            </div>
          ) : file.status === "submitted" || file.status === "underReview" ? (
            <Button variant="secondary" onClick={() => void act("complete", "mark-complete", {}, "أبلغنا الفريق باستكمال ملفك.")} loading={busy === "complete"} className="self-start">
              أبلغ الفريق باستكمال الملف
            </Button>
          ) : null}
          {file.canWithdraw ? (
            <Button variant="sensitive" onClick={() => setWithdrawOpen(true)} className="self-start">سحب الطلب</Button>
          ) : null}
        </div>
      ))}

      <Card title="سجل الطلب">
        <Timeline events={file.events} />
      </Card>

      <Dialog open={withdrawOpen} onClose={() => setWithdrawOpen(false)} title="سحب طلب البيع"
        footer={
          <div className="flex gap-2">
            <Button variant="sensitive" loading={busy === "withdraw"} onClick={() => void act("withdraw", "withdraw", { reason: withdrawReason }, "سحبت الطلب.").then(() => setWithdrawOpen(false))}>تأكيد السحب</Button>
            <Button variant="secondary" onClick={() => setWithdrawOpen(false)}>تراجع</Button>
          </div>
        }>
        <p className="m-0 mb-3 text-15">يتوقف العمل على الطلب، ويبقى في سجلك. يمكنك بدء طلب جديد لاحقًا.</p>
        <Textarea label="السبب (اختياري)" value={withdrawReason} onChange={(e) => setWithdrawReason(e.target.value)} rows={3} maxLength={500} />
      </Dialog>
    </div>
  );
}
