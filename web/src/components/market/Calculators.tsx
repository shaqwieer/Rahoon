"use client";

import Link from "next/link";
import { useState, type FormEvent, type ReactNode } from "react";
import { Chips } from "@/components/market/DynamicField";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Amount } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { toLatinDigits } from "@/lib/market/numbers";
import type { TermsResult } from "@/lib/market/types";
import { DatePicker } from "@/components/ui/DatePicker";
import { Dropdown } from "@/components/ui/Dropdown";
import { UnitInput } from "@/components/ui/UnitInput";

const num = (v: string): number | null => (v.trim() === "" ? null : Number(toLatinDigits(v).replace(/[^\d.]/g, "")));

function MoneyIn({ id, label, value, onChange, help }: { id: string; label: string; value: string; onChange: (v: string) => void; help?: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-14 font-semibold">{label}</label>
      <UnitInput id={id} size="lg" value={value} onChange={onChange} placeholder="فارغ = غير معروف" />
      {help ? <span className="text-12 text-muted">{help}</span> : null}
    </div>
  );
}

type Tab = "developer" | "financier" | "buyer";

/**
 * Three linked calculators with one set of rules (the API's MarketCalculator): developer exit, bank-financed sale, and buyer
 * capacity. An empty field is unknown, never 0, and the result then says what is missing. Nothing here is an offer.
 */
export function Calculators({ initial = "developer" }: { initial?: Tab }) {
  const [tab, setTab] = useState<Tab>(initial);
  return (
    <div className="flex flex-col gap-5">
      <div role="tablist" aria-label="الحاسبات" className="flex flex-wrap gap-2">
        {([["developer", "خروج من التزام لدى مطور"], ["financier", "بيع عقار مموّل"], ["buyer", "قدرتي الشرائية"]] as const).map(([k, l]) => (
          <button key={k} role="tab" type="button" aria-selected={tab === k} onClick={() => setTab(k)}
            className={cn("min-h-11 rounded-pill border px-4 text-15 font-semibold", tab === k ? "border-rust bg-rust text-white" : "border-line-strong bg-white hover:bg-subtle")}>
            {l}
          </button>
        ))}
      </div>
      <div role="tabpanel">
        {tab === "developer" ? <DeveloperCalc /> : tab === "financier" ? <FinancierCalc /> : <CapacityCalc />}
      </div>
      <p className="m-0 text-13 leading-6 text-muted">
        النتائج تقديرية بحسب ما تدخله، وتحتاج مراجعة فريق رهون. لا تعني عرضًا أو سعر بيع أو موافقة تمويل، ولا تشمل عمولة رهون ما لم تُعتمد سياستها.
      </p>
    </div>
  );
}

type Comparison = {
  comparable: boolean;
  reason?: string;
  text?: string;
  difference?: number;
  stale?: boolean;
  reference: { value: number; date: string; source: string };
  notes: string[];
};

function useCalc() {
  const [result, setResult] = useState<TermsResult | null>(null);
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const run = async (body: unknown) => {
    setBusy(true);
    setError(null);
    try {
      const r = await apiSend<{ result: TermsResult; comparison: Comparison | null }>("POST", "/market/calc/terms", body);
      setResult(r.result);
      setComparison(r.comparison);
    } catch (err) {
      setError(isApiError(err) ? (err.errors ? Object.values(err.errors)[0]?.[0] ?? err.title : err.title) : "تعذّر الحساب الآن. تحقق من الاتصال.");
    } finally {
      setBusy(false);
    }
  };
  return { result, comparison, error, busy, run };
}

function DeveloperCalc() {
  const [f, setF] = useState({ paid: "", balance: "", arrearsState: "none", arrears: "", inBalance: "yes", payer: "buyer", reduction: "0", buyerNow: "0", sellerCosts: "0" });
  const set = (k: keyof typeof f) => (v: string) => setF((x) => ({ ...x, [k]: v }));
  const { result, comparison, error, busy, run } = useCalc();
  const [x, setX] = useState<Extras>(NO_EXTRAS);
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void run({
      ...extrasBody(x),
      developer: {
        paidApproved: num(f.paid), remainingBalance: num(f.balance), arrearsState: f.arrearsState, arrears: f.arrearsState === "has" ? num(f.arrears) : null,
        arrearsInBalance: f.arrearsState === "has" ? f.inBalance : null, arrearsPayer: f.payer, reduction: num(f.reduction) ?? 0,
      },
      sellerCosts: num(f.sellerCosts), buyerCostsNow: num(f.buyerNow), buyerCostsLater: 0,
    });
  };
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <form onSubmit={submit} className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
        <MoneyIn id="d-paid" label="المدفوع المعتمد من ثمن الوحدة (P)" value={f.paid} onChange={set("paid")} help="كما في كشف المطور، دون الرسوم والغرامات." />
        <MoneyIn id="d-bal" label="الرصيد المتبقي للمطور (D)" value={f.balance} onChange={set("balance")} />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-14 font-semibold">متأخرات (A)</legend>
          <Chips name="arr" value={f.arrearsState} onChange={set("arrearsState")} options={[{ value: "none", label: "لا توجد" }, { value: "has", label: "توجد" }, { value: "unknown", label: "لا أعرف" }]} />
        </fieldset>
        {f.arrearsState === "has" ? (
          <>
            <MoneyIn id="d-arr" label="إجمالي المتأخرات" value={f.arrears} onChange={set("arrears")} />
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-14 font-semibold">هل هي ضمن الرصيد المتبقي؟</legend>
              <Chips name="inbal" value={f.inBalance} onChange={set("inBalance")} options={[{ value: "yes", label: "نعم" }, { value: "no", label: "لا" }, { value: "unknown", label: "لا أعرف" }]} />
            </fieldset>
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-14 font-semibold">من يتحمل المتأخرات عند الإتمام؟</legend>
              <Chips name="payer" value={f.payer} onChange={set("payer")} options={[{ value: "buyer", label: "المشتري" }, { value: "seller", label: "صاحب العقار" }]} />
            </fieldset>
          </>
        ) : null}
        <MoneyIn id="d-red" label="تخفيض تقبله من مدفوعك (V)" value={f.reduction} onChange={set("reduction")} help="اكتب 0 إذا لا يوجد." />
        <MoneyIn id="d-bnow" label="تكاليف المشتري المستحقة الآن" value={f.buyerNow} onChange={set("buyerNow")} help="مثل رسوم النقل لدى المطور إن كانت معروفة." />
        <MoneyIn id="d-sc" label="تكاليف يتحملها صاحب العقار" value={f.sellerCosts} onChange={set("sellerCosts")} />
        <ExtrasFields x={x} set={setX} buyerSide />
        <Button type="submit" loading={busy} className="self-start">احسب</Button>
      </form>
      <div className="flex flex-col gap-3">
        {error ? <Alert tone="err">{error}</Alert> : null}
        {result ? <div className="rounded-lg border border-line bg-white p-5"><TermsBreakdown result={result} audience="team" /></div> : <Placeholder />}
        <ComparisonBox c={comparison} />
      </div>
    </div>
  );
}

function FinancierCalc() {
  const [f, setF] = useState({ price: "", payoff: "", arrearsState: "none", arrears: "", includes: "yes", sellerCosts: "0" });
  const set = (k: keyof typeof f) => (v: string) => setF((x) => ({ ...x, [k]: v }));
  const { result, comparison, error, busy, run } = useCalc();
  const [x, setX] = useState<Extras>(NO_EXTRAS);
  const submit = (e: FormEvent) => {
    e.preventDefault();
    void run({
      ...extrasBody(x),
      financier: {
        salePrice: num(f.price), payoffAmount: num(f.payoff), arrearsState: f.arrearsState, arrears: f.arrearsState === "has" ? num(f.arrears) : null,
        payoffIncludesArrears: f.arrearsState === "has" ? f.includes : null,
      },
      sellerCosts: num(f.sellerCosts), buyerCostsNow: 0, buyerCostsLater: 0, needsNewFinancing: true,
    });
  };
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <form onSubmit={submit} className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
        <MoneyIn id="f-price" label="سعر البيع المقترح" value={f.price} onChange={set("price")} />
        <MoneyIn id="f-payoff" label="مبلغ السداد المطلوب من الجهة" value={f.payoff} onChange={set("payoff")} help="من خطاب الجهة. لا تحسبه من الأقساط المتبقية؛ اتركه فارغًا إن لم يتوفر." />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-14 font-semibold">متأخرات لدى الجهة</legend>
          <Chips name="farr" value={f.arrearsState} onChange={set("arrearsState")} options={[{ value: "none", label: "لا توجد" }, { value: "has", label: "توجد" }, { value: "unknown", label: "لا أعرف" }]} />
        </fieldset>
        {f.arrearsState === "has" ? (
          <>
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
              <legend className="mb-1 text-14 font-semibold">هل يشملها مبلغ السداد؟</legend>
              <Chips name="incl" value={f.includes} onChange={set("includes")} options={[{ value: "yes", label: "نعم" }, { value: "no", label: "لا" }, { value: "unknown", label: "لا أعرف" }]} />
            </fieldset>
            {f.includes === "no" ? <MoneyIn id="f-arr" label="إجمالي المتأخرات" value={f.arrears} onChange={set("arrears")} /> : null}
          </>
        ) : null}
        <MoneyIn id="f-sc" label="تكاليف يتحملها صاحب العقار" value={f.sellerCosts} onChange={set("sellerCosts")} />
        <ExtrasFields x={x} set={setX} buyerSide={false} />
        <Button type="submit" loading={busy} className="self-start">احسب</Button>
      </form>
      <div className="flex flex-col gap-3">
        {error ? <Alert tone="err">{error}</Alert> : null}
        {result ? <div className="rounded-lg border border-line bg-white p-5"><TermsBreakdown result={result} audience="owner" /></div> : <Placeholder />}
        <ComparisonBox c={comparison} />
      </div>
    </div>
  );
}

function CapacityCalc() {
  const [f, setF] = useState({ now: "", inst: "", freq: "monthly", max: "", months: "60" });
  const set = (k: keyof typeof f) => (v: string) => setF((x) => ({ ...x, [k]: v }));
  const [res, setRes] = useState<{
    monthlyEquivalent: number | null; capacityTotal: number | null; annualComfort: number | null; notes: string[];
    published: { fitting: number; incomplete: number; comparable: number; total: number };
  } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      setRes(await apiSend("POST", "/market/calc/capacity", { availableNow: num(f.now), installmentComfort: num(f.inst), installmentFrequency: f.freq, maxPrice: num(f.max), commitMonths: num(f.months) }));
    } catch (err) {
      setError(isApiError(err) ? err.title : "تعذّر الحساب الآن.");
    } finally {
      setBusy(false);
    }
  };
  const q = new URLSearchParams();
  if (num(f.now)) q.set("maxNow", String(num(f.now)));
  if (num(f.inst)) { q.set("maxInstallment", String(num(f.inst))); if (f.freq !== "monthly") q.set("freq", f.freq); }
  if (num(f.max)) q.set("maxTotal", String(num(f.max)));
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <form onSubmit={submit} className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
        <MoneyIn id="c-now" label="المبلغ المتاح لديك الآن" value={f.now} onChange={set("now")} />
        <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-3">
          <MoneyIn id="c-inst" label="القسط المريح لك" value={f.inst} onChange={set("inst")} />
          <div className="flex flex-col gap-1.5">
            <span id="c-freq-l" className="text-14 font-semibold">الدورية</span>
            <Dropdown ariaLabel="الدورية" size="lg" className="w-36" value={f.freq} onChange={set("freq")} options={[{ value: "monthly", label: "شهري" }, { value: "quarterly", label: "ربع سنوي" }, { value: "semiannual", label: "نصف سنوي" }, { value: "annual", label: "سنوي" }]} />
          </div>
        </div>
        <MoneyIn id="c-max" label="الحد الأقصى للإجمالي (اختياري)" value={f.max} onChange={set("max")} />
        <div className="flex flex-col gap-1.5">
          <label htmlFor="c-months" className="text-14 font-semibold">مدة الالتزام بالأقساط (بالأشهر)</label>
          <input id="c-months" inputMode="numeric" dir="ltr" value={f.months} onChange={(e) => set("months")(e.target.value)} className="min-h-12 rounded-sm border border-line-strong bg-white px-3 text-end" />
        </div>
        <Button type="submit" loading={busy} className="self-start">احسب</Button>
      </form>
      <div className="flex flex-col gap-3">
        {error ? <Alert tone="err">{error}</Alert> : null}
        {res ? (
          <div className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
            {res.monthlyEquivalent !== null ? <p className="m-0 text-15">مكافئ قسطك شهريًا للمقارنة: <Amount value={res.monthlyEquivalent} strong /></p> : null}
            {res.capacityTotal !== null ? <p className="m-0 text-15">ما تستطيع الالتزام به خلال المدة (المتاح الآن + الأقساط): <Amount value={res.capacityTotal} strong /></p> : <p className="m-0 text-14 text-muted">أدخل المبلغ المتاح والقسط والمدة لتقدير الإجمالي.</p>}
            {res.annualComfort !== null ? <p className="m-0 text-15">ما يريحك في السنة (مع أي دفعة سنوية): <Amount value={res.annualComfort} strong /></p> : null}
            <p className="m-0 text-15">
              من {res.published.total} {res.published.total === 1 ? "فرصة منشورة" : "فرص منشورة"}: <strong>{res.published.fitting}</strong> تناسب هذه الأرقام
              {res.published.incomplete ? `، و${res.published.incomplete} أرقامها غير مكتملة فلا يمكن الحكم عليها` : ""}.
            </p>
            <Link href={`/opportunities?${q}`} className={buttonClasses({ variant: "primary", size: "md", className: "self-start" })}>عرض الفرص بهذه الأرقام</Link>
            <ul className="m-0 ps-5 text-13 text-muted">{res.notes.map((n) => <li key={n}>{n}</li>)}</ul>
          </div>
        ) : <Placeholder />}
      </div>
    </div>
  );
}

function Placeholder() {
  return <div className="flex min-h-40 items-center justify-center rounded-lg border border-dashed border-line-strong bg-white p-6 text-center text-14 text-muted">أدخل الأرقام ثم اضغط «احسب». الحقل الفارغ يعني «غير معروف» ولا يُحسب صفرًا.</div>;
}

// ── Phase 2: fees with who pays them, the date of the figures, and a dated market reference ──

type Fee = { label: string; amount: string; payer: "buyer" | "seller" | "split"; share: string; timing: "now" | "later" };
type Extras = { fees: Fee[]; asOf: string; refValue: string; refDate: string; refSource: string };
const NO_EXTRAS: Extras = { fees: [], asOf: "", refValue: "", refDate: "", refSource: "" };

function extrasBody(x: Extras) {
  return {
    fees: x.fees.length
      ? x.fees.map((f) => ({ label: f.label.trim() || "رسم", amount: num(f.amount), payer: f.payer, buyerSharePercent: f.payer === "split" ? (num(f.share) ?? 50) : null, timing: f.timing }))
      : null,
    asOf: x.asOf || null,
    reference: x.refValue || x.refDate || x.refSource ? { value: num(x.refValue), date: x.refDate || null, source: x.refSource || null } : null,
  };
}

function ExtrasFields({ x, set, buyerSide }: { x: Extras; set: (x: Extras) => void; buyerSide: boolean }) {
  const fee = (i: number, p: Partial<Fee>) => set({ ...x, fees: x.fees.map((f, j) => (j === i ? { ...f, ...p } : f)) });
  return (
    <div className="flex flex-col gap-4 border-t border-divider pt-4">
      <fieldset className="m-0 flex flex-col gap-3 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">رسوم ومن يتحملها</legend>
        <span className="text-12 text-muted">أضف الرسوم التي تعرفها (أمثلة تجربها، لا رسوم معتمدة). المبلغ الفارغ «غير معروف» ويجعل النتيجة غير مكتملة.</span>
        {x.fees.map((f, i) => (
          <div key={i} className="flex flex-col gap-2 rounded-md bg-subtle p-3">
            <div className="grid gap-2 sm:grid-cols-2">
              <input aria-label="اسم الرسم" value={f.label} onChange={(e) => fee(i, { label: e.target.value })} placeholder="مثال: رسوم النقل لدى المطور" className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15" />
              <MoneyIn id={`fee-${i}`} label="المبلغ" value={f.amount} onChange={(v) => fee(i, { amount: v })} />
            </div>
            <Chips name={`payer-${i}`} value={f.payer} onChange={(v) => fee(i, { payer: v as Fee["payer"] })}
              options={[{ value: "buyer", label: "المشتري" }, { value: "seller", label: "صاحب العقار" }, { value: "split", label: "مقسوم" }]} />
            {f.payer === "split" ? (
              <label className="flex items-center gap-2 text-14">حصة المشتري %
                <input inputMode="numeric" dir="ltr" value={f.share} onChange={(e) => fee(i, { share: e.target.value })} className="min-h-10 w-20 rounded-sm border border-line-strong bg-white px-2 text-end" />
              </label>
            ) : null}
            {buyerSide && f.payer !== "seller" ? (
              <Chips name={`timing-${i}`} value={f.timing} onChange={(v) => fee(i, { timing: v as Fee["timing"] })}
                options={[{ value: "now", label: "عند الإتمام" }, { value: "later", label: "لاحقًا" }]} />
            ) : null}
            <button type="button" onClick={() => set({ ...x, fees: x.fees.filter((_, j) => j !== i) })} className="self-start text-13 font-semibold text-err underline">إزالة الرسم</button>
          </div>
        ))}
        {x.fees.length < 10 ? (
          <Button type="button" variant="secondary" size="md" className="self-start" onClick={() => set({ ...x, fees: [...x.fees, { label: "", amount: "", payer: "buyer", share: "50", timing: "now" }] })}>أضف رسمًا</Button>
        ) : null}
      </fieldset>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="asof" className="text-14 font-semibold">الأرقام كما في تاريخ (اختياري)</label>
        <DatePicker id="asof" size="lg" value={x.asOf} onChange={(v) => set({ ...x, asOf: v })} />
      </div>
      <details className="rounded-md border border-line p-3">
        <summary className="cursor-pointer text-14 font-semibold">قارن الإجمالي بمرجع سوقي مؤرخ (اختياري)</summary>
        <div className="mt-3 flex flex-col gap-3">
          <MoneyIn id="ref-v" label="قيمة المرجع لعقار مماثل" value={x.refValue} onChange={(v) => set({ ...x, refValue: v })} />
          <div className="flex flex-col gap-1.5">
            <label htmlFor="ref-d" className="text-14 font-semibold">تاريخ المرجع</label>
            <DatePicker id="ref-d" size="lg" value={x.refDate} onChange={(v) => set({ ...x, refDate: v })} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="ref-s" className="text-14 font-semibold">مصدر المرجع</label>
            <input id="ref-s" value={x.refSource} onChange={(e) => set({ ...x, refSource: e.target.value })} placeholder="مثال: صفقة مسجلة في الحي نفسه" className="min-h-12 rounded-sm border border-line-strong bg-white px-3" />
          </div>
          <span className="text-12 text-muted">المقارنة فرق تقديري مع مرجع تذكره أنت، وليست توفيرًا مضمونًا أو عائدًا.</span>
        </div>
      </details>
    </div>
  );
}

function ComparisonBox({ c }: { c: Comparison | null }) {
  if (!c) return null;
  return (
    <div className="flex flex-col gap-2 rounded-lg border border-info-line bg-info-bg p-4 text-14">
      <strong>المقارنة بالمرجع</strong>
      {c.comparable ? <span>{c.text}</span> : <span>{c.reason}</span>}
      <span className="text-13 text-charcoal">المرجع: {c.reference.source} بتاريخ <bdi dir="ltr">{c.reference.date}</bdi></span>
      <ul className="m-0 ps-5 text-13 text-muted">{c.notes.map((n) => <li key={n}>{n}</li>)}</ul>
    </div>
  );
}
