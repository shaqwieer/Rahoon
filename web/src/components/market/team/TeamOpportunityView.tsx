"use client";

import Link from "next/link";
import { useState } from "react";
import { Chips } from "@/components/market/DynamicField";
import { LocationMap } from "@/components/market/LocationMap";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Badge, Card, StatusBadge, Timeline } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, DateField, MonthField, Select, Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { day } from "@/lib/market/format";
import { toLatinDigits } from "@/lib/market/numbers";
import type { MarketEvent, Photo, TermsView } from "@/lib/market/types";
import { Dropdown } from "@/components/ui/Dropdown";

interface DevIn { paidApproved: number | null; remainingBalance: number | null; arrearsState: string; arrears: number | null; arrearsInBalance: string | null; arrearsPayer: string; reduction: number; installment: number | null; installmentFrequency: string | null; remainingInstallments: number | null; extraPayment: number | null; extraPaymentRecurrence: string | null; extraPaymentDate: string | null }
interface FinIn { salePrice: number | null; payoffAmount: number | null; payoffValidUntil: string | null; arrearsState: string; arrears: number | null; payoffIncludesArrears: string | null }
interface TermsIn { developer: DevIn | null; financier: FinIn | null; sellerCosts: number | null; buyerCostsNow: number | null; buyerCostsLater: number | null; needsNewFinancing: boolean; states: Record<string, string> }

export interface TeamOpportunity {
  reference: string;
  status: string;
  statusLabel: string;
  saleRequest: string;
  editable: { title: string; description: string | null; area: number | null; bedrooms: number | null; bathrooms: number | null; readiness: string | null; deliveryMonth: string | null; specs: Record<string, string>; features: string[]; photoIds: string[]; locationPrecision: string };
  content: { propertyType: string; propertyTypeLabel: string; track: string; trackLabel: string; cityLabel: string; district: string | null };
  location: { exact: { lat: number | null; lng: number | null }; public: { lat: number | null; lng: number | null }; precision: "exact" | "approximate"; ownerWish: string | null };
  availablePhotos: Photo[];
  draftTerms: TermsView | null;
  draftInput: TermsIn | null;
  publishedTerms: TermsView | null;
  versions: { id: string; versionNo: number; status: string; preparedByLabel: string; sentToOwnerAt: string | null; ownerDecidedAt: string | null; ownerNote: string | null; current: boolean; published: boolean }[];
  checklist: { key: string; label: string; done: boolean }[];
  blockers: string[];
  interests: { reference: string; status: string; statusLabel: string; createdAt: string; contactName: string | null }[];
  events: MarketEvent[];
  obligations: { id: string; kind: string; partyName: string; approval: { status: string; statusLabel: string; conditions: string | null } | null }[];
  publicUrl: string | null;
  actions: { edit: boolean; sendToOwner: boolean; publish: boolean; pause: boolean; resume: boolean; withdraw: boolean; checklist: boolean };
}

const TERMS_STATUS: Record<string, string> = { draft: "مسودة", sentToOwner: "عند المالك", ownerConfirmed: "أكده المالك", ownerRequestedChanges: "طلب المالك تعديله", superseded: "استُبدل" };
const nz = (v: string): number | null => (v.trim() === "" ? null : Number(toLatinDigits(v).replace(/[^\d.]/g, "")));
const s = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n));

function Num({ label, value, onChange, help }: { label: string; value: string; onChange: (v: string) => void; help?: string }) {
  return <TextField label={label} value={value} onChange={(e) => onChange(e.target.value)} inputMode="numeric" ltr help={help ?? "فارغ = غير معروف"} />;
}
function StateSel({ value, onChange }: { value: string | undefined; onChange: (v: string) => void }) {
  return (
    <Dropdown ariaLabel="حالة الرقم" size="sm" className="w-full" value={value ?? "declared"} onChange={onChange}
      options={[{ value: "verified", label: "موثّق" }, { value: "declared", label: "حسب المالك" }, { value: "estimated", label: "تقدير" }]} />
  );
}

/** Opportunity preparation: content and photos, figures (versioned terms), owner confirmation, checklist, publish/pause/withdraw. */
export function TeamOpportunityView({ o }: { o: TeamOpportunity }) {
  const { busy, error, setError, run } = useTeamAction();
  const base = `/team/market/opportunities/${o.reference}`;
  const e = o.editable;
  const [c, setC] = useState({ title: e.title, description: e.description ?? "", area: s(e.area), bedrooms: s(e.bedrooms), bathrooms: s(e.bathrooms), readiness: e.readiness ?? "", deliveryMonth: e.deliveryMonth ?? "", locationPrecision: e.locationPrecision });
  const [photoIds, setPhotoIds] = useState<string[]>(e.photoIds);
  const inp = o.draftInput;
  const [t, setT] = useState(() => ({
    paid: s(inp?.developer?.paidApproved), balance: s(inp?.developer?.remainingBalance), devArrState: inp?.developer?.arrearsState ?? "none", devArr: s(inp?.developer?.arrears),
    inBalance: inp?.developer?.arrearsInBalance ?? "yes", payer: inp?.developer?.arrearsPayer ?? "buyer", reduction: s(inp?.developer?.reduction ?? 0), inst: s(inp?.developer?.installment),
    freq: inp?.developer?.installmentFrequency ?? "monthly", remaining: s(inp?.developer?.remainingInstallments), extra: s(inp?.developer?.extraPayment), extraRec: inp?.developer?.extraPaymentRecurrence ?? "annual",
    extraDate: inp?.developer?.extraPaymentDate ?? "", price: s(inp?.financier?.salePrice), payoff: s(inp?.financier?.payoffAmount), payoffUntil: inp?.financier?.payoffValidUntil ?? "",
    finArrState: inp?.financier?.arrearsState ?? "none", finArr: s(inp?.financier?.arrears), includes: inp?.financier?.payoffIncludesArrears ?? "yes",
    sellerCosts: s(inp?.sellerCosts), buyerNow: s(inp?.buyerCostsNow), buyerLater: s(inp?.buyerCostsLater), needsNew: inp?.needsNewFinancing ?? o.content.track !== "developer",
    transfer: o.draftTerms?.transferConditions ?? o.publishedTerms?.transferConditions ?? "", scope: o.draftTerms?.verificationScope ?? o.publishedTerms?.verificationScope ?? "",
    verifiedOn: o.draftTerms?.verifiedOn ?? "",
  }));
  const [states, setStates] = useState<Record<string, string>>(inp?.states ?? {});
  const [checks, setChecks] = useState(o.checklist.filter((x) => x.done).map((x) => x.key));
  const [dlg, setDlg] = useState<null | "pause" | "withdraw">(null);
  const [reason, setReason] = useState("");
  const dev = o.content.track !== "financier";
  const fin = o.content.track !== "developer";
  const set = (k: keyof typeof t) => (v: string) => setT((x) => ({ ...x, [k]: v }));

  const saveContent = () =>
    run("content", "PUT", `${base}/content`, {
      title: c.title, description: c.description, area: nz(c.area), bedrooms: nz(c.bedrooms), bathrooms: nz(c.bathrooms), readiness: c.readiness || null,
      deliveryMonth: c.deliveryMonth || null, photoIds, locationPrecision: c.locationPrecision,
    });
  const saveTerms = () =>
    run("terms", "PUT", `${base}/terms`, {
      developer: dev ? {
        paidApproved: nz(t.paid), remainingBalance: nz(t.balance), arrearsState: t.devArrState, arrears: t.devArrState === "has" ? nz(t.devArr) : null,
        arrearsInBalance: t.devArrState === "has" ? t.inBalance : null, arrearsPayer: t.payer, reduction: nz(t.reduction) ?? 0, installment: nz(t.inst),
        installmentFrequency: t.inst ? t.freq : null, remainingInstallments: nz(t.remaining), extraPayment: nz(t.extra), extraPaymentRecurrence: t.extra ? t.extraRec : null,
        extraPaymentDate: t.extra && t.extraDate ? t.extraDate : null,
      } : null,
      financier: fin ? {
        salePrice: nz(t.price), payoffAmount: nz(t.payoff), payoffValidUntil: t.payoffUntil || null, arrearsState: t.finArrState,
        arrears: t.finArrState === "has" ? nz(t.finArr) : null, payoffIncludesArrears: t.finArrState === "has" ? t.includes : null,
      } : null,
      sellerCosts: nz(t.sellerCosts), buyerCostsNow: nz(t.buyerNow), buyerCostsLater: nz(t.buyerLater), needsNewFinancing: t.needsNew, states,
      transferConditions: t.transfer || null, verificationScope: t.scope || null, verifiedOn: t.verifiedOn || null,
    });

  const togglePhoto = (id: string) => setPhotoIds((x) => (x.includes(id) ? x.filter((y) => y !== id) : [...x, id]));
  const stateFor = (k: string) => <StateSel value={states[k]} onChange={(v) => setStates((x) => ({ ...x, [k]: v }))} />;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/team/opportunities" className="text-14 font-semibold">← الفرص</Link>
          <h1 className="m-0 flex flex-wrap items-center gap-2 text-24 font-bold"><bdi dir="ltr" className="font-mono">{o.reference}</bdi><StatusBadge status={o.status} label={o.statusLabel} /></h1>
          <span className="text-14 text-muted">من طلب البيع <Link href={`/team/sale/${o.saleRequest}`}><bdi dir="ltr">{o.saleRequest}</bdi></Link> · {o.content.trackLabel}</span>
        </div>
        <div className="flex flex-wrap gap-2">
          {o.publicUrl ? <Link href={o.publicUrl} target="_blank" className="inline-flex min-h-10 items-center gap-1 rounded-sm border border-line px-3 text-14"><Icon name="open_in_new" size={16} />الصفحة العامة</Link> : null}
          {o.actions.sendToOwner ? <Button onClick={() => void run("send", "POST", `${base}/send-to-owner`)} loading={busy === "send"}>إرسال الملخص للمالك</Button> : null}
          {o.actions.publish ? <Button onClick={() => void run("publish", "POST", `${base}/publish`)} loading={busy === "publish"}>{o.status === "published" || o.status === "paused" ? "نشر النسخة المحدثة" : "نشر الفرصة"}</Button> : null}
          {o.actions.pause ? <Button variant="secondary" onClick={() => { setReason(""); setError(null); setDlg("pause"); }}>إيقاف مؤقت</Button> : null}
          {o.actions.resume ? <Button variant="secondary" onClick={() => void run("resume", "POST", `${base}/resume`)} loading={busy === "resume"}>إعادة العرض</Button> : null}
          {o.actions.withdraw ? <Button variant="sensitive" onClick={() => { setReason(""); setError(null); setDlg("withdraw"); }}>سحب</Button> : null}
        </div>
      </div>
      <ActionError error={dlg ? null : error} />
      {o.blockers.length ? (
        <Alert tone="warn" title="قبل النشر">
          <ul className="m-0 ps-5">{o.blockers.map((b) => <li key={b}>{b}</li>)}</ul>
        </Alert>
      ) : o.status !== "published" ? <Alert tone="ok">كل شروط النشر مكتملة.</Alert> : null}

      <div className="grid gap-5 xl:grid-cols-2">
        <Card title="المحتوى والصور والموقع" actions={o.actions.edit ? <Button size="sm" onClick={() => void saveContent()} loading={busy === "content"}>حفظ المحتوى</Button> : null}>
          <div className="flex flex-col gap-4">
            <TextField label="العنوان" value={c.title} onChange={(ev) => setC({ ...c, title: ev.target.value })} />
            <Textarea label="الوصف (لا بيانات شخصية)" value={c.description} onChange={(ev) => setC({ ...c, description: ev.target.value })} rows={4} maxLength={3000} />
            <div className="grid grid-cols-3 gap-3">
              <TextField label="المساحة م²" value={c.area} onChange={(ev) => setC({ ...c, area: ev.target.value })} ltr inputMode="decimal" />
              {o.content.propertyType !== "land" && o.content.propertyType !== "office" && o.content.propertyType !== "shop" ? <TextField label="غرف النوم" value={c.bedrooms} onChange={(ev) => setC({ ...c, bedrooms: ev.target.value })} ltr inputMode="numeric" /> : null}
              {o.content.propertyType !== "land" ? <TextField label="دورات المياه" value={c.bathrooms} onChange={(ev) => setC({ ...c, bathrooms: ev.target.value })} ltr inputMode="numeric" /> : null}
            </div>
            {o.content.propertyType !== "land" ? (
              <div className="grid grid-cols-2 gap-3">
                <label className="flex flex-col gap-1 text-14 font-semibold">الحالة
                  <Dropdown ariaLabel="الحالة" value={c.readiness} onChange={(v) => setC({ ...c, readiness: v })} options={[{ value: "ready", label: "جاهز" }, { value: "under_construction", label: "تحت الإنشاء" }]} />
                </label>
                {c.readiness === "under_construction" ? <MonthField label="شهر التسليم" value={c.deliveryMonth} onValueChange={(v) => setC({ ...c, deliveryMonth: v })} /> : null}
              </div>
            ) : null}
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-14 font-semibold">دقة الموقع المعروض</legend>
              <Chips name="prec" value={c.locationPrecision} onChange={(v) => setC({ ...c, locationPrecision: v })}
                options={[{ value: "approximate", label: "منطقة تقريبية" }, { value: "exact", label: "الموقع الدقيق" }]} />
              <span className="text-12 text-muted">رغبة المالك: {o.location.ownerWish === "exact" ? "الموقع الدقيق" : "منطقة تقريبية"}. لا يُعرض الدقيق دون موافقته.</span>
            </fieldset>
            {o.location.public.lat !== null && o.location.public.lng !== null ? <LocationMap lat={o.location.public.lat} lng={o.location.public.lng} precision={o.location.precision} height={200} /> : <p className="m-0 text-14 text-warn">لا يوجد موقع.</p>}
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-14 font-semibold">الصور المعروضة (بالترتيب، الأولى غلاف)</legend>
              <ul className="m-0 grid list-none grid-cols-3 gap-2 p-0">
                {o.availablePhotos.map((p) => {
                  const idx = photoIds.indexOf(p.id);
                  return (
                    <li key={p.id}>
                      <button type="button" onClick={() => togglePhoto(p.id)} aria-pressed={idx >= 0}
                        className={cn("relative block w-full overflow-hidden rounded-sm border-2", idx >= 0 ? "border-rust" : "border-transparent opacity-60")}>
                        {/* eslint-disable-next-line @next/next/no-img-element -- team preview */}
                        <img src={p.url} alt="" className="aspect-[4/3] w-full object-cover" />
                        {idx >= 0 ? <span className="absolute start-1 top-1 rounded-pill bg-rust px-2 text-12 text-white">{idx === 0 ? "غلاف" : idx + 1}</span> : null}
                        {p.reviewStatus !== "accepted" ? <span className="absolute inset-x-0 bottom-0 bg-warn-bg text-12 text-warn">{p.reviewStatus === "rejected" ? "مرفوضة" : "لم تُقبل بعد"}</span> : null}
                      </button>
                    </li>
                  );
                })}
              </ul>
            </fieldset>
          </div>
        </Card>

        <Card title={`الأرقام وشروط النقل${o.draftTerms ? ` — الإصدار ${o.draftTerms.versionNo} (${TERMS_STATUS[o.draftTerms.status] ?? o.draftTerms.status})` : ""}`}
          actions={o.actions.edit ? <Button size="sm" onClick={() => void saveTerms()} loading={busy === "terms"}>حفظ وحساب</Button> : null}>
          <div className="flex flex-col gap-4">
            {o.draftTerms && o.draftTerms.status !== "draft" ? <p className="m-0 text-13 text-info">أي تعديل يُنشئ إصدارًا جديدًا يحتاج تأكيد المالك ثم إعادة النشر. النسخة المنشورة لا تتغير بصمت.</p> : null}
            {dev ? (
              <fieldset className="m-0 flex flex-col gap-3 rounded-md border border-line p-3">
                <legend className="px-1 text-14 font-bold">جزء المطور</legend>
                <div className="grid grid-cols-2 gap-3">
                  <div className="flex min-w-0 flex-col gap-1.5"><Num label="المدفوع المعتمد P" value={t.paid} onChange={set("paid")} />{stateFor("paid_approved")}</div>
                  <div className="flex min-w-0 flex-col gap-1.5"><Num label="الرصيد المتبقي D" value={t.balance} onChange={set("balance")} />{stateFor("remaining_balance")}</div>
                </div>
                <Chips name="devarr" value={t.devArrState} onChange={set("devArrState")} options={[{ value: "none", label: "لا متأخرات" }, { value: "has", label: "متأخرات" }, { value: "unknown", label: "غير معروف" }]} />
                {t.devArrState === "has" ? (
                  <div className="grid grid-cols-2 gap-3">
                    <div className="flex min-w-0 flex-col gap-1.5"><Num label="المتأخرات A" value={t.devArr} onChange={set("devArr")} />{stateFor("arrears")}</div>
                    <Select label="ضمن D؟" value={t.inBalance} onValueChange={set("inBalance")} options={[{ value: "yes", label: "نعم" }, { value: "no", label: "لا" }, { value: "unknown", label: "غير معروف" }]} />
                    <Select label="يتحملها" value={t.payer} onValueChange={set("payer")} options={[{ value: "buyer", label: "المشتري" }, { value: "seller", label: "صاحب العقار" }]} />
                  </div>
                ) : null}
                <Num label="تخفيض يقبله المالك V" value={t.reduction} onChange={set("reduction")} help="0 إذا لا يوجد" />
                <div className="grid grid-cols-3 gap-3">
                  <div className="col-span-2 flex min-w-0 flex-col gap-1.5"><Num label="القسط" value={t.inst} onChange={set("inst")} />{stateFor("installment_amount")}</div>
                  <Select label="الدورية" value={t.freq} onValueChange={set("freq")} options={[{ value: "monthly", label: "شهري" }, { value: "quarterly", label: "ربع سنوي" }, { value: "semiannual", label: "نصف سنوي" }, { value: "annual", label: "سنوي" }]} />
                </div>
                <Num label="عدد الأقساط المتبقية" value={t.remaining} onChange={set("remaining")} />
                <div className="grid grid-cols-3 gap-3">
                  <Num label="دفعة إضافية" value={t.extra} onChange={set("extra")} help="ضمن D، ليست من القسط" />
                  <Select label="التكرار" value={t.extraRec} onValueChange={set("extraRec")} options={[{ value: "annual", label: "سنويًا" }, { value: "once", label: "مرة واحدة" }]} />
                  <DateField label="القادمة" hijri={false} value={t.extraDate} onValueChange={set("extraDate")} />
                </div>
              </fieldset>
            ) : null}
            {fin ? (
              <fieldset className="m-0 flex flex-col gap-3 rounded-md border border-line p-3">
                <legend className="px-1 text-14 font-bold">جزء جهة التمويل</legend>
                <div className="grid grid-cols-2 gap-3">
                  <div className="flex min-w-0 flex-col gap-1.5"><Num label="سعر البيع" value={t.price} onChange={set("price")} />{stateFor("sale_price")}</div>
                  <div className="flex min-w-0 flex-col gap-1.5"><Num label="مبلغ السداد (من الخطاب)" value={t.payoff} onChange={set("payoff")} />{stateFor("payoff_amount")}</div>
                </div>
                <DateField label="صلاحية مبلغ السداد" value={t.payoffUntil} onValueChange={set("payoffUntil")} />
                <Chips name="finarr" value={t.finArrState} onChange={set("finArrState")} options={[{ value: "none", label: "لا متأخرات" }, { value: "has", label: "متأخرات" }, { value: "unknown", label: "غير معروف" }]} />
                {t.finArrState === "has" ? (
                  <div className="grid grid-cols-2 gap-3">
                    <Select label="يشملها مبلغ السداد؟" value={t.includes} onValueChange={set("includes")} options={[{ value: "yes", label: "نعم" }, { value: "no", label: "لا" }, { value: "unknown", label: "غير معروف" }]} />
                    {t.includes === "no" ? <Num label="المتأخرات" value={t.finArr} onChange={set("finArr")} /> : null}
                  </div>
                ) : null}
                <Checkbox checked={t.needsNew} onChange={(ev) => setT((x) => ({ ...x, needsNew: ev.target.checked }))} label="قد يحتاج المشتري تمويلًا جديدًا (يُعرض مع حالته)" />
              </fieldset>
            ) : null}
            <div className="grid grid-cols-3 gap-3">
              <div className="flex min-w-0 flex-col gap-1.5"><Num label="تكاليف المشتري الآن" value={t.buyerNow} onChange={set("buyerNow")} />{stateFor("buyer_costs_now")}</div>
              <Num label="تكاليف المشتري لاحقًا" value={t.buyerLater} onChange={set("buyerLater")} />
              <div className="flex min-w-0 flex-col gap-1.5"><Num label="تكاليف المالك" value={t.sellerCosts} onChange={set("sellerCosts")} />{stateFor("seller_costs")}</div>
            </div>
            <Textarea label="شروط النقل (تُنشر)" value={t.transfer} onChange={(ev) => set("transfer")(ev.target.value)} rows={2} />
            <Textarea label="نطاق ما راجعه الفريق (يُنشر مع الشارة)" value={t.scope} onChange={(ev) => set("scope")(ev.target.value)} rows={2} />
            <DateField label="تاريخ المراجعة" value={t.verifiedOn} onValueChange={set("verifiedOn")} />
            {o.draftTerms ? <div className="rounded-md bg-warm p-3"><TermsBreakdown result={o.draftTerms} audience="team" /></div> : null}
          </div>
        </Card>
      </div>

      <div className="grid gap-5 xl:grid-cols-3">
        <Card title="قائمة التحقق قبل النشر" actions={o.actions.checklist ? <Button size="sm" variant="secondary" onClick={() => void run("check", "POST", `${base}/checklist`, { items: checks })} loading={busy === "check"}>حفظ</Button> : null}>
          <div className="flex flex-col gap-2">
            {o.checklist.map((x) => (
              <Checkbox key={x.key} checked={checks.includes(x.key)} disabled={!o.actions.checklist} onChange={(ev) => setChecks((cs) => (ev.target.checked ? [...cs, x.key] : cs.filter((y) => y !== x.key)))} label={x.label} />
            ))}
          </div>
          <ul className="m-0 mt-3 flex list-none flex-col gap-1 p-0 text-13">
            {o.obligations.map((ob) => <li key={ob.id}>موافقة {ob.kind === "developer" ? "المطور" : "جهة التمويل"} ({ob.partyName}): <Badge tone={ob.approval?.status === "approved" ? "ok" : "neutral"}>{ob.approval?.statusLabel ?? "لم تُطلب"}</Badge></li>)}
          </ul>
        </Card>
        <Card title="الإصدارات">
          <ul className="m-0 flex list-none flex-col gap-2 p-0 text-14">
            {o.versions.map((v) => (
              <li key={v.id} className="flex flex-col rounded-md bg-subtle px-3 py-2">
                <span className="font-semibold">الإصدار {v.versionNo} {v.published ? <Badge tone="ok">منشور</Badge> : null} {v.current ? <Badge tone="info">الحالي</Badge> : null}</span>
                <span className="text-12 text-muted">{TERMS_STATUS[v.status] ?? v.status} · {v.preparedByLabel}{v.sentToOwnerAt ? ` · أُرسل ${day(v.sentToOwnerAt)}` : ""}{v.ownerDecidedAt ? ` · قرار المالك ${day(v.ownerDecidedAt)}` : ""}</span>
                {v.ownerNote ? <span className="text-13">ملاحظة المالك: {v.ownerNote}</span> : null}
              </li>
            ))}
          </ul>
        </Card>
        <Card title="الاهتمامات">
          {o.interests.length === 0 ? <p className="m-0 text-14 text-muted">لا توجد اهتمامات.</p> : (
            <ul className="m-0 flex list-none flex-col gap-1 p-0">
              {o.interests.map((i) => <li key={i.reference}><Link href={`/team/interests/${i.reference}`} className="flex justify-between gap-2 rounded-sm p-2 hover:bg-subtle"><span>{i.contactName ?? i.reference}</span><StatusBadge status={i.status} label={i.statusLabel} /></Link></li>)}
            </ul>
          )}
        </Card>
      </div>
      {o.publishedTerms && o.draftTerms && o.publishedTerms.id !== o.draftTerms.id ? (
        <Card title={`النسخة المنشورة حاليًا (الإصدار ${o.publishedTerms.versionNo})`}><TermsBreakdown result={o.publishedTerms} audience="buyer" /></Card>
      ) : null}
      <Card title="السجل"><Timeline events={o.events} /></Card>

      <ActionDialog open={dlg === "pause"} onClose={() => setDlg(null)} title="إيقاف العرض مؤقتًا" confirm="إيقاف" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/pause`, { reason })}>
        <ActionError error={error} />
        <Textarea label="السبب (يظهر للمالك)" value={reason} onChange={(ev) => setReason(ev.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "withdraw"} onClose={() => setDlg(null)} title="سحب الفرصة" confirm="سحب" tone="sensitive" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/withdraw`, { reason })}>
        <ActionError error={error} />
        <Textarea label="السبب (يظهر للمالك)" value={reason} onChange={(ev) => setReason(ev.target.value)} rows={2} />
      </ActionDialog>
    </div>
  );
}
