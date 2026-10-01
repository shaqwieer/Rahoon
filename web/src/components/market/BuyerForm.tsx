"use client";

import { Chips } from "@/components/market/DynamicField";
import { toLatinDigits } from "@/lib/market/numbers";
import type { Catalog } from "@/lib/market/types";

export interface BuyerValues {
  availableNow: string;
  installmentComfort: string;
  installmentFrequency: string;
  maxPrice: string;
  purchaseMode: string;
  cities: string[];
  areasText: string;
  propertyTypes: string[];
  areaMin: string;
  areaMax: string;
  bedroomsMin: string;
  readiness: string;
  deliveryBy: string;
}

export const emptyBuyer: BuyerValues = {
  availableNow: "", installmentComfort: "", installmentFrequency: "monthly", maxPrice: "", purchaseMode: "", cities: [], areasText: "",
  propertyTypes: [], areaMin: "", areaMax: "", bedroomsMin: "", readiness: "any", deliveryBy: "",
};

const num = (v: string) => (v.trim() === "" ? null : Number(toLatinDigits(v).replace(/[^\d.]/g, "")));
const RES = ["apartment", "duplex", "villa", "townhouse"];

/** API body for PUT/POST buyer requests (amounts parsed from Arabic or Latin digits). */
export function buyerBody(v: BuyerValues, extra?: Record<string, unknown>) {
  const residential = v.propertyTypes.length === 0 || v.propertyTypes.some((t) => RES.includes(t));
  return {
    availableNow: num(v.availableNow), installmentComfort: num(v.installmentComfort), installmentFrequency: v.installmentComfort ? v.installmentFrequency : null,
    maxPrice: num(v.maxPrice), purchaseMode: v.purchaseMode || null, cities: v.cities, areasText: v.areasText.trim() || null, propertyTypes: v.propertyTypes,
    areaMin: num(v.areaMin), areaMax: num(v.areaMax), bedroomsMin: residential ? num(v.bedroomsMin) : null, readiness: v.readiness || null,
    deliveryBy: v.readiness === "ready" ? null : v.deliveryBy || null, ...extra,
  };
}

export function validateBuyer(v: BuyerValues, part: "capacity" | "preferences"): Record<string, string> {
  const e: Record<string, string> = {};
  if (part === "capacity") {
    if (num(v.availableNow) === null || Number.isNaN(num(v.availableNow))) e.availableNow = "أدخل المبلغ المتاح لديك الآن بالريال.";
    if (v.installmentComfort && Number.isNaN(num(v.installmentComfort))) e.installmentComfort = "أدخل القسط بالأرقام.";
    if (v.maxPrice && Number.isNaN(num(v.maxPrice))) e.maxPrice = "أدخل الحد الأقصى بالأرقام.";
    if (!v.purchaseMode) e.purchaseMode = "اختر طريقة الشراء.";
  } else {
    if (v.cities.length === 0) e.cities = "اختر مدينة واحدة على الأقل.";
    if (v.propertyTypes.length === 0) e.propertyTypes = "اختر نوع عقار واحدًا على الأقل.";
    const a = num(v.areaMin), b = num(v.areaMax);
    if (a !== null && b !== null && a > b) e.areaMax = "الحد الأعلى أقل من الحد الأدنى.";
  }
  return e;
}

const inputCls = "min-h-12 w-full rounded-sm border border-line-strong bg-white px-3 text-16 aria-[invalid=true]:border-err";

function Money({ id, label, help, value, onChange, error, optional }: { id: string; label: string; help?: string; value: string; onChange: (v: string) => void; error?: string; optional?: boolean }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-15 font-semibold">
        {label}
        {optional ? <span className="ms-1 text-13 font-normal text-muted">(اختياري)</span> : null}
      </label>
      <div className="relative">
        <input id={id} inputMode="numeric" dir="ltr" className={`${inputCls} ps-12 text-end tabular-nums`} value={value} aria-invalid={Boolean(error) || undefined}
          onChange={(e) => onChange(e.target.value)} />
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-14 text-muted">ر.س</span>
      </div>
      {help ? <span className="text-13 text-muted">{help}</span> : null}
      {error ? <span className="text-13 text-err">{error}</span> : null}
    </div>
  );
}

/** Capacity section: the amount available now is separate from the price, and installments are separate from both. */
export function CapacityFields({ v, set, errors, catalog }: { v: BuyerValues; set: (p: Partial<BuyerValues>) => void; errors: Record<string, string>; catalog: Catalog }) {
  return (
    <div className="flex flex-col gap-5">
      <Money id="b-availableNow" label="المبلغ المتاح لديك الآن" help="ما تستطيع دفعه عند الإتمام، وليس سعر العقار." value={v.availableNow} onChange={(x) => set({ availableNow: x })} error={errors.availableNow} />
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto]">
        <Money id="b-installment" label="القسط المريح لك" optional value={v.installmentComfort} onChange={(x) => set({ installmentComfort: x })} error={errors.installmentComfort}
          help="إذا كنت تقبل إكمال أقساط لدى المطور." />
        <div className="flex flex-col gap-1.5">
          <label htmlFor="b-freq" className="text-15 font-semibold">دوريته</label>
          <select id="b-freq" className={inputCls} value={v.installmentFrequency} onChange={(e) => set({ installmentFrequency: e.target.value })}>
            {catalog.frequencies.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
          </select>
        </div>
      </div>
      <Money id="b-maxPrice" label="الحد الأقصى لإجمالي ما تدفعه" optional value={v.maxPrice} onChange={(x) => set({ maxPrice: x })} error={errors.maxPrice}
        help="المبلغ الآن مع الأقساط والدفعات اللاحقة." />
      <fieldset id="b-purchaseMode" className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-15 font-semibold">كيف ستشتري؟</legend>
        <Chips name="purchaseMode" value={v.purchaseMode} invalid={Boolean(errors.purchaseMode)} onChange={(x) => set({ purchaseMode: x })}
          options={[{ value: "cash", label: "نقدًا" }, { value: "external_finance", label: "بتمويل من جهة خارجية" }, { value: "undecided", label: "لم أقرر بعد" }]} />
        {v.purchaseMode === "external_finance" ? <span className="text-13 text-muted">موافقة التمويل تصدر من جهة التمويل نفسها، ولا يغني عنها اعتماد رهون لملفك.</span> : null}
        {errors.purchaseMode ? <span className="text-13 text-err">{errors.purchaseMode}</span> : null}
      </fieldset>
    </div>
  );
}

export function PreferenceFields({ v, set, errors, catalog }: { v: BuyerValues; set: (p: Partial<BuyerValues>) => void; errors: Record<string, string>; catalog: Catalog }) {
  const toggle = (list: string[], x: string) => (list.includes(x) ? list.filter((y) => y !== x) : [...list, x]);
  const residential = v.propertyTypes.length === 0 || v.propertyTypes.some((t) => RES.includes(t));
  return (
    <div className="flex flex-col gap-5">
      <fieldset id="b-cities" className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-15 font-semibold">المدن</legend>
        <Chips name="cities" multiple options={catalog.cities.map((c) => ({ value: c.key, label: c.label }))} value={v.cities} invalid={Boolean(errors.cities)}
          onChange={(x) => set({ cities: toggle(v.cities, x) })} />
        {errors.cities ? <span className="text-13 text-err">{errors.cities}</span> : null}
      </fieldset>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="b-areas" className="text-15 font-semibold">أحياء أو مشاريع تفضلها <span className="text-13 font-normal text-muted">(اختياري)</span></label>
        <input id="b-areas" className={inputCls} value={v.areasText} onChange={(e) => set({ areasText: e.target.value })} placeholder="مثال: النرجس، الياسمين" />
      </div>
      <fieldset id="b-propertyTypes" className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-15 font-semibold">نوع العقار (يمكن اختيار أكثر من نوع)</legend>
        <Chips name="types" multiple options={catalog.propertyTypes} value={v.propertyTypes} invalid={Boolean(errors.propertyTypes)} onChange={(x) => set({ propertyTypes: toggle(v.propertyTypes, x) })} />
        {errors.propertyTypes ? <span className="text-13 text-err">{errors.propertyTypes}</span> : null}
      </fieldset>
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="b-amin" className="text-15 font-semibold">أقل مساحة (م²)</label>
          <input id="b-amin" inputMode="numeric" dir="ltr" className={`${inputCls} text-end`} value={v.areaMin} onChange={(e) => set({ areaMin: e.target.value })} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="b-amax" className="text-15 font-semibold">أكبر مساحة (م²)</label>
          <input id="b-amax" inputMode="numeric" dir="ltr" className={`${inputCls} text-end`} value={v.areaMax} onChange={(e) => set({ areaMax: e.target.value })} aria-invalid={Boolean(errors.areaMax) || undefined} />
          {errors.areaMax ? <span className="text-13 text-err">{errors.areaMax}</span> : null}
        </div>
        {residential ? (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="b-beds" className="text-15 font-semibold">غرف النوم (على الأقل)</label>
            <select id="b-beds" className={inputCls} value={v.bedroomsMin} onChange={(e) => set({ bedroomsMin: e.target.value })}>
              <option value="">أي عدد</option>
              {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}+</option>)}
            </select>
          </div>
        ) : null}
      </div>
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-15 font-semibold">جاهز أم تحت الإنشاء؟</legend>
        <Chips name="readiness" value={v.readiness} onChange={(x) => set({ readiness: x })}
          options={[{ value: "any", label: "لا يهم" }, { value: "ready", label: "جاهز فقط" }, { value: "under_construction", label: "تحت الإنشاء" }]} />
      </fieldset>
      {v.readiness !== "ready" ? (
        <div className="flex flex-col gap-1.5 sm:max-w-[260px]">
          <label htmlFor="b-delivery" className="text-15 font-semibold">موعد الاستلام المفضل (قبل) <span className="text-13 font-normal text-muted">(اختياري)</span></label>
          <input id="b-delivery" type="month" dir="ltr" className={`${inputCls} text-end`} value={v.deliveryBy} onChange={(e) => set({ deliveryBy: e.target.value })} />
        </div>
      ) : null}
    </div>
  );
}
