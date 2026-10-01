"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { buyerBody, CapacityFields, emptyBuyer, PreferenceFields, validateBuyer, type BuyerValues } from "@/components/market/BuyerForm";
import { PhoneSignIn } from "@/components/market/PhoneSignIn";
import { Amount, SaveState } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { ErrorSummary } from "@/components/ui/ErrorSummary";
import { Checkbox, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { cityLabel, label } from "@/lib/market/catalog";
import { toLatinDigits } from "@/lib/market/numbers";
import type { BuyerRequestView, Catalog } from "@/lib/market/types";

const STORE = "rahoon.buy.draft.v1";
const STEPS = ["القدرة الشرائية", "تفضيلات العقار", "التواصل والتأكيد"];
interface Draft { v: 1; clientDraftId: string; step: 1 | 2 | 3; values: BuyerValues; name: string }
const uuid = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`);

function read(): Draft | null {
  try {
    const d = JSON.parse(window.localStorage.getItem(STORE) ?? "null") as Draft | null;
    return d?.v === 1 ? d : null;
  } catch {
    return null;
  }
}
function write(d: Draft | null) {
  try {
    if (d) window.localStorage.setItem(STORE, JSON.stringify(d));
    else window.localStorage.removeItem(STORE);
  } catch {
    /* storage unavailable: the form still works */
  }
}

/** The buyer request in three steps (capacity, preferences, contact). Kept on this device until the mobile sign-in. */
export function BuyerWizard({ catalog, signedIn: initiallySignedIn, userName }: { catalog: Catalog; signedIn: boolean; userName: string | null }) {
  const [d, setD] = useState<Draft>(() => ({ v: 1, clientDraftId: uuid(), step: 1, values: emptyBuyer, name: userName ?? "" }));
  const [loaded, setLoaded] = useState(false);
  const [signedIn, setSignedIn] = useState(initiallySignedIn);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [attempt, setAttempt] = useState(0);
  const [accept, setAccept] = useState(false);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [done, setDone] = useState<{ reference: string; statusLabel: string; nextStep: string } | null>(null);
  const [existing, setExisting] = useState<string | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    const saved = read();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time restore from device storage after hydration
    if (saved) setD({ ...saved, name: saved.name || userName || "" });
    setLoaded(true);
  }, [userName]);

  const update = (patch: Partial<Draft>) =>
    setD((x) => {
      const next = { ...x, ...patch };
      write(next);
      return next;
    });
  const setValues = (p: Partial<BuyerValues>) => update({ values: { ...d.values, ...p } });

  const go = (step: 1 | 2 | 3) => {
    update({ step });
    setErrors({});
    requestAnimationFrame(() => heading.current?.focus());
  };
  const next = () => {
    const e = validateBuyer(d.values, d.step === 1 ? "capacity" : "preferences");
    setErrors(e);
    setAttempt((n) => n + 1);
    if (!Object.keys(e).length) go((d.step + 1) as 2 | 3);
  };

  const submit = async () => {
    const e: Record<string, string> = {};
    if (d.name.trim().length < 2) e.contactName = "اكتب اسمك.";
    if (!accept) e.acceptDeclarations = "وافق على معالجة طلبك والتواصل معك بشأنه.";
    setErrors(e);
    setAttempt((n) => n + 1);
    if (Object.keys(e).length) return;
    setBusy(true);
    setFormError(null);
    try {
      const created = await apiSend<{ reference: string; created: boolean; request: BuyerRequestView }>("POST", "/market/buyer-requests", buyerBody(d.values, { clientDraftId: d.clientDraftId, contactName: d.name.trim() }));
      if (!created.created && created.request.status !== "draft") {
        // A live buyer request already exists: edit it from the account instead of creating a second one.
        setExisting(created.reference);
        return;
      }
      if (!created.created) await apiSend("PUT", `/market/buyer-requests/${created.reference}`, buyerBody(d.values, { contactName: d.name.trim() }));
      const res = await apiSend<{ reference: string; statusLabel: string; nextStep: string }>("POST", `/market/buyer-requests/${created.reference}/submit`, { contactName: d.name.trim(), acceptDeclarations: true });
      write(null);
      setDone(res);
    } catch (err) {
      if (isApiError(err) && err.errors) {
        const fe = Object.fromEntries(Object.entries(err.errors).map(([k, v]) => [k, v[0]]));
        setErrors(fe);
        setAttempt((n) => n + 1);
        const k = Object.keys(fe)[0];
        go(["availableNow", "installmentComfort", "installmentFrequency", "maxPrice", "purchaseMode"].includes(k) ? 1 : ["contactName", "acceptDeclarations"].includes(k) ? 3 : 2);
      } else setFormError(isApiError(err) && err.title ? err.title : "تعذّر الإرسال. بياناتك محفوظة على هذا الجهاز؛ أعد المحاولة.");
    } finally {
      setBusy(false);
    }
  };

  const summary = useMemo(() => Object.entries(errors).map(([k, message]) => ({ fieldId: `b-${k}`, message })), [errors]);
  const n = (v: string) => (v ? Number(toLatinDigits(v).replace(/[^\d.]/g, "")) : null);

  if (existing)
    return (
      <Alert tone="info" title="لديك طلب شراء قائم">
        <p className="m-0 mb-2">لكل حساب طلب شراء واحد قائم. عدّل تفضيلاتك وقدرتك الشرائية من حسابك.</p>
        <Link href="/account/buy" className={buttonClasses({ variant: "primary", size: "md" })}>طلب الشراء <bdi dir="ltr">{existing}</bdi></Link>
      </Alert>
    );
  if (done)
    return (
      <div className="flex flex-col gap-4 rounded-lg border border-ok-line bg-ok-bg p-5 md:p-7" role="status">
        <Icon name="check_circle" size={36} filled className="text-ok" />
        <h2 className="m-0 text-24 font-bold">استلمنا طلب الشراء</h2>
        <p className="m-0 text-15">رقم الطلب <bdi dir="ltr" className="font-mono font-bold">{done.reference}</bdi> · الحالة: {done.statusLabel}</p>
        <p className="m-0 text-15"><strong>الخطوة التالية: </strong>{done.nextStep}</p>
        <div className="flex flex-wrap gap-3">
          <Link href="/account/buy" className={buttonClasses({ variant: "primary", size: "lg" })}>الفرص المقترحة لك</Link>
          <Link href="/opportunities" className={buttonClasses({ variant: "secondary", size: "lg" })}>تصفح كل الفرص</Link>
        </div>
      </div>
    );
  if (!loaded) return <div className="h-64 animate-rh-pulse rounded-lg bg-subtle" />;

  return (
    <div className="flex flex-col gap-5">
      <ol className="m-0 grid list-none grid-cols-3 gap-2 p-0" aria-label="خطوات الطلب">
        {STEPS.map((s, i) => (
          <li key={s} aria-current={i + 1 === d.step ? "step" : undefined} className="flex flex-col gap-1.5">
            <span className={cn("h-1.5 rounded-pill", i + 1 <= d.step ? "bg-rust" : "bg-track")} />
            <span className={cn("text-12 md:text-13", i + 1 === d.step ? "font-bold" : "text-muted")}>{i + 1}. {s}</span>
          </li>
        ))}
      </ol>
      <div className="flex items-center justify-between gap-2">
        <h2 ref={heading} tabIndex={-1} className="m-0 text-22 font-bold outline-none">{STEPS[d.step - 1]}</h2>
        {!signedIn ? <SaveState state="device" /> : null}
      </div>
      <ErrorSummary errors={summary} focusKey={attempt} />
      {formError ? <Alert tone="err">{formError}</Alert> : null}
      {d.step === 1 ? <CapacityFields v={d.values} set={setValues} errors={errors} catalog={catalog} /> : null}
      {d.step === 2 ? <PreferenceFields v={d.values} set={setValues} errors={errors} catalog={catalog} /> : null}
      {d.step === 3 ? (
        !signedIn ? (
          <section className="flex flex-col gap-3 rounded-md border border-line bg-white p-4 md:p-5">
            <h3 className="m-0 text-17 font-bold">ادخل برقم جوالك لإرسال الطلب</h3>
            <p className="m-0 text-14 text-muted">إذا كان لديك حساب بنفس الرقم (لطلب بيع مثلًا) سنستخدمه نفسه.</p>
            <PhoneSignIn askName initialName={d.name} compact submitLabel="تحقق ومتابعة" onSignedIn={(r) => { if (r.name) update({ name: r.name }); setSignedIn(true); }} />
          </section>
        ) : (
          <div className="flex flex-col gap-4">
            <div id="b-contactName">
              <TextField label="الاسم" value={d.name} onChange={(e) => update({ name: e.target.value })} error={errors.contactName} autoComplete="name" />
            </div>
            <section className="flex flex-col gap-2 rounded-md border border-line bg-warm p-4 text-15">
              <div className="flex items-center justify-between">
                <strong>ملخص طلبك</strong>
                <Button variant="text" onClick={() => go(1)}>تعديل</Button>
              </div>
              <span>المتاح الآن: <Amount value={n(d.values.availableNow)} strong /></span>
              {d.values.installmentComfort ? <span>القسط المريح: <Amount value={n(d.values.installmentComfort)} /> {label(catalog.frequencies, d.values.installmentFrequency)}</span> : null}
              <span>المدن: {d.values.cities.map((c) => cityLabel(catalog, c)).join("، ")}</span>
              <span>الأنواع: {d.values.propertyTypes.map((t) => label(catalog.propertyTypes, t)).join("، ")}</span>
            </section>
            <div id="b-acceptDeclarations">
              <Checkbox checked={accept} onChange={(e) => setAccept(e.target.checked)} error={errors.acceptDeclarations}
                label="أوافق على أن يعالج فريق رهون طلبي ويتواصل معي بشأن الفرص المناسبة. الأرقام التي أدخلتها تصريح مني وليست موافقة تمويل." />
            </div>
          </div>
        )
      ) : null}
      <div className="sticky bottom-0 z-10 -mx-4 flex items-center justify-between gap-3 border-t border-line bg-white/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0">
        {d.step > 1 ? <Button variant="secondary" onClick={() => go((d.step - 1) as 1 | 2)}><Icon name="arrow_forward" size={18} mirror />السابق</Button> : <span />}
        {d.step < 3 ? <Button onClick={next}>التالي<Icon name="arrow_back" size={18} mirror /></Button> : signedIn ? <Button onClick={() => void submit()} loading={busy}>إرسال الطلب</Button> : null}
      </div>
    </div>
  );
}
