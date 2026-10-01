"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { DynamicField, Chips } from "@/components/market/DynamicField";
import { OrgPicker } from "@/components/market/OrgPicker";
import { PhoneSignIn } from "@/components/market/PhoneSignIn";
import { Amount, Badge, SaveState } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { ErrorSummary } from "@/components/ui/ErrorSummary";
import { Checkbox, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { cityLabel, isAnswered, label, obligationFields, prune, UNKNOWN } from "@/lib/market/catalog";
import type { Answers, Catalog, SaveResponse, TermsResult } from "@/lib/market/types";

type Kind = "developer" | "financier";
interface ObDraft {
  key: string;
  id?: string;
  kind: Kind;
  partyId: string | null;
  /** Directory name shown for partyId (the API records its own copy). */
  partyName?: string;
  partyOther: string;
  notInList: boolean;
  relationNote: string;
  answers: Answers;
}
interface SellDraft {
  v: 1;
  clientDraftId: string;
  reference?: string;
  step: 1 | 2 | 3;
  propertyType?: string;
  city?: string;
  district?: string;
  project?: string;
  obligationMode?: "developer" | "financier" | "multiple";
  obligations: ObDraft[];
  name: string;
  email: string;
  relationship?: "owner" | "authorized";
}

const STORE = "rahoon.sell.draft.v1";
const uuid = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`);
const newOb = (kind: Kind): ObDraft => ({ key: uuid(), kind, partyId: null, partyOther: "", notInList: false, relationNote: "", answers: {} });

function readDraft(): SellDraft | null {
  try {
    const raw = window.localStorage.getItem(STORE);
    if (!raw) return null;
    const d = JSON.parse(raw) as SellDraft;
    return d?.v === 1 && d.clientDraftId ? d : null;
  } catch {
    return null;
  }
}
function writeDraft(d: SellDraft | null) {
  try {
    if (d) window.localStorage.setItem(STORE, JSON.stringify(d));
    else window.localStorage.removeItem(STORE);
  } catch {
    /* private mode / blocked storage: the form still works, it just isn't kept on the device */
  }
}

const STEPS = ["العقار والجهة", "الأرقام الأساسية", "التواصل والتأكيد"];

/**
 * The first sale request in three short steps (docs/product/product-definition.md §4). Before sign-in the draft lives on
 * this device only (and says so); after the mobile sign-in in step 3 it is saved to the account under the same draft id,
 * so a retry or a second tab never creates a second request.
 */
export function SaleWizard({ catalog, signedIn: signedInInitially, userName }: { catalog: Catalog; signedIn: boolean; userName: string | null }) {
  const [d, setD] = useState<SellDraft>(() => ({ v: 1, clientDraftId: uuid(), step: 1, obligations: [], name: userName ?? "", email: "" }));
  const [loaded, setLoaded] = useState(false);
  const [signedIn, setSignedIn] = useState(signedInInitially);
  const [save, setSave] = useState<"idle" | "device" | "saving" | "saved" | "failed">("idle");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [attempt, setAttempt] = useState(0);
  const [accept, setAccept] = useState({ declarations: false, processing: false });
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState<{ reference: string; statusLabel: string; nextStep: string } | null>(null);
  const [estimate, setEstimate] = useState<TermsResult | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const syncing = useRef(false);
  const dirty = useRef(false);

  // Restore the device draft once mounted (never during SSR).
  useEffect(() => {
    const saved = readDraft();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time restore from device storage after hydration
    if (saved) setD({ ...saved, name: saved.name || userName || "" });
    setLoaded(true);
  }, [userName]);

  const update = useCallback((patch: Partial<SellDraft> | ((x: SellDraft) => SellDraft)) => {
    setD((x) => {
      const next = typeof patch === "function" ? patch(x) : { ...x, ...patch };
      writeDraft(next);
      return next;
    });
    dirty.current = true;
    setSave((s) => (s === "saving" ? s : "device"));
  }, []);

  const payload = useCallback(
    (x: SellDraft) => ({
      clientDraftId: x.clientDraftId,
      propertyType: x.propertyType ?? null,
      city: x.city ?? null,
      district: x.district ?? "",
      project: x.project ?? "",
      obligationMode: x.obligationMode ?? null,
      obligations: x.obligations.map((o) => ({
        id: o.id ?? null,
        kind: o.kind,
        partyId: o.notInList ? null : o.partyId,
        partyOtherName: o.notInList ? o.partyOther : o.partyId ? null : "",
        relationNote: o.relationNote,
        answers: prune(catalog, "obligation", o.answers, null, o.kind),
      })),
      contactName: x.name || null,
      contactEmail: x.email ?? "",
    }),
    [catalog],
  );

  // Server autosave once signed in (create under the device draft id, then save the same request).
  const sync = useCallback(async () => {
    if (!signedIn || syncing.current) return null;
    syncing.current = true;
    dirty.current = false;
    setSave("saving");
    try {
      const x = readDraft() ?? d;
      const body = payload(x);
      const res = x.reference
        ? await apiSend<SaveResponse>("PUT", `/market/sale-requests/${x.reference}`, body)
        : // The API returns the same request for the same draft id, so a retry never duplicates it.
          await apiSend<SaveResponse>("POST", "/market/sale-requests", body);
      const ids = res.file.obligations.map((o) => o.id);
      setD((cur) => {
        const next = { ...cur, reference: res.file.reference, obligations: cur.obligations.map((o, i) => ({ ...o, id: ids[i] ?? o.id })) };
        writeDraft(next);
        return next;
      });
      setSave("saved");
      return res;
    } catch (err) {
      setSave("failed");
      dirty.current = true;
      if (isApiError(err) && err.code === "locked") setFormError(err.title);
      return null;
    } finally {
      syncing.current = false;
    }
  }, [signedIn, d, payload]);

  useEffect(() => {
    if (!loaded || !signedIn || !dirty.current || done) return;
    const t = window.setTimeout(() => void sync(), 900);
    return () => window.clearTimeout(t);
  }, [d, loaded, signedIn, sync, done]);

  // Preliminary estimate from the figures typed so far (same engine as the team; costs unknown until reviewed).
  useEffect(() => {
    if (d.step !== 2 || d.obligations.length === 0) return;
    const ctrl = new AbortController();
    const num = (a: Answers, k: string) => (a[k] && a[k] !== UNKNOWN && !Number.isNaN(Number(a[k])) ? Number(a[k]) : null);
    const dev = d.obligations.find((o) => o.kind === "developer");
    const fin = d.obligations.find((o) => o.kind === "financier");
    const target = dev ? num(dev.answers, "owner_target") : null;
    const paid = dev ? num(dev.answers, "paid_approved") : null;
    const body = {
      developer: dev
        ? {
            paidApproved: paid, remainingBalance: num(dev.answers, "remaining_balance"), arrearsState: dev.answers.arrears_state ?? "unknown", arrears: num(dev.answers, "arrears_amount"),
            arrearsInBalance: dev.answers.arrears_in_balance ?? null, arrearsPayer: "buyer", reduction: paid !== null && target !== null && target < paid ? paid - target : 0,
            installment: num(dev.answers, "installment_amount"), installmentFrequency: dev.answers.installment_frequency && dev.answers.installment_frequency !== UNKNOWN ? dev.answers.installment_frequency : null,
          }
        : null,
      financier: fin
        ? { salePrice: num(fin.answers, "asking_price"), payoffAmount: num(fin.answers, "payoff_amount"), arrearsState: fin.answers.arrears_state ?? "unknown", arrears: num(fin.answers, "arrears_amount"), payoffIncludesArrears: fin.answers.payoff_includes_arrears ?? null }
        : null,
      sellerCosts: null, buyerCostsNow: null, buyerCostsLater: null, needsNewFinancing: Boolean(fin),
    };
    const t = window.setTimeout(() => {
      apiSend<TermsResult>("POST", "/market/calc/terms", body, { signal: ctrl.signal }).then(setEstimate).catch(() => setEstimate(null));
    }, 500);
    return () => {
      ctrl.abort();
      window.clearTimeout(t);
    };
  }, [d.step, d.obligations]);

  const city = catalog.cities.find((c) => c.key === d.city);

  const setMode = (mode: SellDraft["obligationMode"]) =>
    update((x) => {
      let obs = x.obligations;
      if (mode === "developer" || mode === "financier") {
        const keep = obs[0];
        obs = [keep ? { ...keep, kind: mode, partyId: keep.kind === mode ? keep.partyId : null, partyName: keep.kind === mode ? keep.partyName : undefined, answers: prune(catalog, "obligation", keep.answers, null, mode) } : newOb(mode)];
      } else if (mode === "multiple") {
        obs = obs.length >= 2 ? obs : [...obs, ...[newOb("developer"), newOb("financier")].slice(obs.length)];
        if (obs.length === 1) obs = [...obs, newOb(obs[0].kind === "developer" ? "financier" : "developer")];
      }
      return { ...x, obligationMode: mode, obligations: obs };
    });

  const setOb = (key: string, patch: Partial<ObDraft>) =>
    update((x) => ({
      ...x,
      obligations: x.obligations.map((o) => {
        if (o.key !== key) return o;
        const next = { ...o, ...patch };
        if (patch.kind && patch.kind !== o.kind) next.answers = prune(catalog, "obligation", o.answers, null, patch.kind);
        if (patch.answers) next.answers = prune(catalog, "obligation", patch.answers, null, next.kind);
        return next;
      }),
    }));

  // ── Validation per step (mirrors the server's submit rules; the server re-checks) ──
  const validate = (step: number): Record<string, string> => {
    const e: Record<string, string> = {};
    if (step === 1) {
      if (!d.propertyType) e.propertyType = "اختر نوع العقار.";
      if (!d.city) e.city = "اختر المدينة.";
      if (!d.district?.trim()) e.district = "اكتب الحي أو اختره.";
      if (!d.obligationMode) e.obligationMode = "اختر جهة الالتزام.";
      d.obligations.forEach((o, i) => {
        if (!o.notInList && !o.partyId) e[`o${i}.party`] = o.kind === "developer" ? "اختر المطور من الدليل أو «غير موجود في الدليل»." : "اختر الجهة من الدليل أو «غير موجودة في الدليل».";
        if (o.notInList && o.partyOther.trim().length < 2) e[`o${i}.party`] = "اكتب اسم الجهة.";
      });
    }
    if (step === 2) {
      d.obligations.forEach((o, i) => {
        for (const f of obligationFields(catalog, o.kind, o.answers).filter((x) => x.submitAnswer && x.initial))
          if (!isAnswered(o.answers, f.key)) e[`o${i}.${f.key}`] = f.allowUnknown ? `أدخل «${f.label}» أو اختر «لا أعرف».` : `اختر إجابة لـ«${f.label}».`;
      });
    }
    if (step === 3) {
      if (d.name.trim().length < 2) e.contactName = "اكتب اسمك.";
      if (!d.relationship) e.relationship = "حدد علاقتك بالعقار.";
      if (!accept.declarations) e.acceptDeclarations = "أقرّ بأنك صاحب العلاقة بالعقار أو مخوّل بذلك.";
      if (!accept.processing) e.acceptProcessing = "وافق على معالجة طلبك والتواصل معك بشأنه.";
      if (d.email && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(d.email.trim())) e.contactEmail = "أدخل بريدًا صحيحًا أو اتركه فارغًا.";
    }
    return e;
  };

  const goTo = (step: 1 | 2 | 3) => {
    update({ step });
    setErrors({});
    window.requestAnimationFrame(() => {
      headingRef.current?.focus();
      headingRef.current?.scrollIntoView({ block: "start", behavior: "smooth" });
    });
  };

  const next = () => {
    const e = validate(d.step);
    setErrors(e);
    setAttempt((n) => n + 1);
    if (Object.keys(e).length === 0) goTo((d.step + 1) as 2 | 3);
  };

  const stepOfError = (key: string): 1 | 2 | 3 => {
    if (["propertyType", "city", "district", "obligationMode"].includes(key) || /^o\d+\.party$/.test(key)) return 1;
    if (/^o\d+\./.test(key)) return 2;
    return 3;
  };

  const submit = async () => {
    const e = validate(3);
    setErrors(e);
    setAttempt((n) => n + 1);
    setFormError(null);
    if (Object.keys(e).length) return;
    setSubmitting(true);
    try {
      // Let an autosave in flight finish, then save the latest state (creates the request under this draft id if needed).
      for (let i = 0; i < 50 && syncing.current; i++) await new Promise((r) => window.setTimeout(r, 100));
      const saved = await sync();
      const reference = saved?.file.reference ?? readDraft()?.reference;
      if (!reference) throw new Error("not_saved");
      if (saved && Object.keys(saved.invalid).length) {
        setErrors(saved.invalid);
        goTo(stepOfError(Object.keys(saved.invalid)[0]));
        return;
      }
      const res = await apiSend<{ reference: string; statusLabel: string; nextStep: string }>(
        "POST",
        `/market/sale-requests/${reference}/submit`,
        { contactName: d.name.trim(), relationship: d.relationship, acceptDeclarations: accept.declarations, acceptProcessing: accept.processing, contactEmail: d.email.trim() || null },
      );
      writeDraft(null);
      setDone(res);
      window.scrollTo({ top: 0, behavior: "smooth" });
    } catch (err) {
      if (isApiError(err) && err.errors) {
        const fe = Object.fromEntries(Object.entries(err.errors).map(([k, v]) => [k, v[0]]));
        setErrors(fe);
        setAttempt((n) => n + 1);
        goTo(stepOfError(Object.keys(fe)[0]));
      } else setFormError(isApiError(err) && err.title ? err.title : "تعذّر الإرسال. طلبك محفوظ؛ تحقق من اتصالك وأعد المحاولة.");
    } finally {
      setSubmitting(false);
    }
  };

  const summaryErrors = useMemo(() => Object.entries(errors).map(([k, message]) => ({ fieldId: `sw-${k.replace(/\./g, "-")}`, message })), [errors]);

  if (done)
    return (
      <div className="flex flex-col gap-5" role="status">
        <div className="flex flex-col gap-3 rounded-lg border border-ok-line bg-ok-bg p-5 md:p-7">
          <span className="flex size-12 items-center justify-center rounded-full bg-white text-ok">
            <Icon name="check_circle" size={30} filled />
          </span>
          <h2 className="m-0 text-24 leading-9 font-bold">استلمنا طلبك</h2>
          <dl className="m-0 grid gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-13 text-muted">رقم الطلب</dt>
              <dd className="m-0 text-20 font-bold">
                <bdi dir="ltr" className="font-mono">{done.reference}</bdi>
              </dd>
            </div>
            <div>
              <dt className="text-13 text-muted">الحالة</dt>
              <dd className="m-0 text-17 font-semibold">{done.statusLabel}</dd>
            </div>
          </dl>
          <p className="m-0 text-15 leading-7">
            <strong>الخطوة التالية: </strong>
            {done.nextStep}
          </p>
          <p className="m-0 text-14 text-muted">تجد الطلب في «حسابي» بالدخول برقم جوالك.</p>
        </div>
        <div className="flex flex-col gap-3 sm:flex-row">
          <Link href={`/account/sell/${done.reference}`} className={buttonClasses({ variant: "primary", size: "lg" })}>
            استكمل ملفك الآن
          </Link>
          <Link href="/account" className={buttonClasses({ variant: "secondary", size: "lg" })}>
            حسابي
          </Link>
        </div>
      </div>
    );

  if (!loaded) return <div className="h-64 animate-rh-pulse rounded-lg bg-subtle" aria-label="جارٍ التحميل" />;

  return (
    <div className="flex flex-col gap-5">
      {/* Progress */}
      <ol className="m-0 grid list-none grid-cols-3 gap-2 p-0" aria-label="خطوات الطلب">
        {STEPS.map((s, i) => {
          const n = (i + 1) as 1 | 2 | 3;
          const state = n < d.step ? "done" : n === d.step ? "current" : "todo";
          return (
            <li key={s} aria-current={state === "current" ? "step" : undefined} className="flex flex-col gap-1.5">
              <span className={cn("h-1.5 rounded-pill", state === "todo" ? "bg-track" : "bg-rust")} />
              <span className={cn("text-12 md:text-13", state === "current" ? "font-bold text-ink" : "text-muted")}>
                {n}. {s}
              </span>
            </li>
          );
        })}
      </ol>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 ref={headingRef} tabIndex={-1} className="m-0 scroll-mt-28 text-22 leading-8 font-bold outline-none">
          {STEPS[d.step - 1]}
        </h2>
        <SaveState state={signedIn ? save : save === "idle" ? "idle" : "device"} />
      </div>
      <ErrorSummary errors={summaryErrors} focusKey={attempt} />
      {formError ? <Alert tone="err">{formError}</Alert> : null}

      {d.step === 1 ? (
        <div className="flex flex-col gap-6">
          <p className="m-0 text-15 leading-7 text-charcoal">اختر نوع العقار ومكانه، والجهة التي لديك التزام معها. لا نحتاج مستندات الآن.</p>
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0" id="sw-propertyType">
            <legend className="mb-2 text-15 font-semibold">نوع العقار</legend>
            <Chips name="propertyType" options={catalog.propertyTypes} value={d.propertyType} invalid={Boolean(errors.propertyType)} onChange={(v) => update({ propertyType: v })} />
            {errors.propertyType ? <span className="text-13 text-err">{errors.propertyType}</span> : null}
          </fieldset>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="sw-city" className="text-15 font-semibold">
                المدينة
              </label>
              <select id="sw-city" value={d.city ?? ""} aria-invalid={Boolean(errors.city) || undefined}
                onChange={(e) => update({ city: e.target.value || undefined })}
                className="min-h-12 rounded-sm border border-line-strong bg-white px-3 text-16 aria-[invalid=true]:border-err">
                <option value="">اختر المدينة</option>
                {catalog.cities.map((c) => (
                  <option key={c.key} value={c.key}>
                    {c.label}
                  </option>
                ))}
              </select>
              {errors.city ? <span className="text-13 text-err">{errors.city}</span> : null}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="sw-district" className="text-15 font-semibold">
                الحي
              </label>
              <input id="sw-district" list="sw-districts" value={d.district ?? ""} onChange={(e) => update({ district: e.target.value })} aria-invalid={Boolean(errors.district) || undefined}
                placeholder={city?.districts.length ? "اكتب أو اختر من القائمة" : "اكتب اسم الحي"} className="min-h-12 rounded-sm border border-line-strong bg-white px-3 text-16 aria-[invalid=true]:border-err" />
              <datalist id="sw-districts">
                {city?.districts.map((x) => <option key={x} value={x} />)}
              </datalist>
              {errors.district ? <span className="text-13 text-err">{errors.district}</span> : null}
            </div>
          </div>
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0" id="sw-obligationMode">
            <legend className="mb-2 text-15 font-semibold">الالتزام لدى</legend>
            <Chips name="obligationMode" options={catalog.obligationModes} value={d.obligationMode} invalid={Boolean(errors.obligationMode)} onChange={(v) => setMode(v as SellDraft["obligationMode"])} />
            {errors.obligationMode ? <span className="text-13 text-err">{errors.obligationMode}</span> : null}
          </fieldset>

          {d.obligations.map((o, i) => (
            <div key={o.key} className="flex flex-col gap-3 rounded-md border border-line bg-warm p-4">
              {d.obligationMode === "multiple" ? (
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <strong className="text-15">الالتزام {i + 1}</strong>
                  <Chips name={`kind-${o.key}`} options={catalog.obligationKinds} value={o.kind} onChange={(v) => setOb(o.key, { kind: v as Kind, partyId: null, partyName: undefined })} />
                </div>
              ) : null}
              <div id={`sw-o${i}-party`}>
                <OrgPicker
                  kind={o.kind}
                  label={o.kind === "developer" ? "اسم المطور" : "اسم البنك أو جهة التمويل"}
                  value={o.notInList ? { id: null, name: o.partyOther } : o.partyId ? { id: o.partyId, name: o.partyName ?? "" } : null}
                  notInList={o.notInList}
                  onChange={(v, other) =>
                    setOb(o.key, other ? { notInList: true, partyId: null, partyName: undefined, partyOther: v?.name ?? "" } : { notInList: false, partyId: v?.id ?? null, partyName: v?.name })
                  }
                  error={errors[`o${i}.party`]}
                />
              </div>
              {d.obligationMode === "multiple" ? (
                <TextField label="علاقة هذا الالتزام بالآخر (اختياري)" help="مثال: البنك موّل جزءًا من ثمن الوحدة لدى المطور." value={o.relationNote}
                  onChange={(e) => setOb(o.key, { relationNote: e.target.value })} />
              ) : null}
            </div>
          ))}
          {d.obligationMode === "multiple" && d.obligations.length < 4 ? (
            <Button variant="secondary" onClick={() => update((x) => ({ ...x, obligations: [...x.obligations, newOb("developer")] }))} className="self-start">
              <Icon name="add" size={18} />
              إضافة جهة أخرى
            </Button>
          ) : null}
          {d.obligationMode === "multiple" && d.obligations.length > 2 ? (
            <Button variant="text" onClick={() => update((x) => ({ ...x, obligations: x.obligations.slice(0, -1) }))} className="self-start">
              حذف آخر جهة
            </Button>
          ) : null}
          {d.propertyType && d.obligationMode === "developer" ? (
            <TextField label="المشروع (اختياري)" value={d.project ?? ""} onChange={(e) => update({ project: e.target.value })} />
          ) : null}
        </div>
      ) : null}

      {d.step === 2 ? (
        <div className="flex flex-col gap-6">
          <p className="m-0 text-15 leading-7 text-charcoal">
            أدخل ما تعرفه فقط. إذا لم تكن متأكدًا من رقم اختر «لا أعرف»؛ سيطلب الفريق المستند المناسب لاحقًا.
          </p>
          {d.obligations.map((o, i) => (
            <section key={o.key} aria-labelledby={`ob-h-${o.key}`} className="flex flex-col gap-4 rounded-md border border-line bg-white p-4 md:p-5">
              <h3 id={`ob-h-${o.key}`} className="m-0 flex flex-wrap items-center gap-2 text-17 font-bold">
                {o.kind === "developer" ? "التزامك لدى المطور" : "تمويلك لدى البنك أو الجهة"}
                <Badge tone="neutral">{o.notInList ? o.partyOther : (o.partyName ?? "")}</Badge>
              </h3>
              {o.kind === "financier" ? (
                <Alert tone="info" compact>
                  لا نحسب مبلغ السداد من الأقساط المتبقية، ولا نفترض أن المشتري سيكمل قسطك الحالي. المبلغ الرسمي يأتي من خطاب الجهة.
                </Alert>
              ) : null}
              <div className="grid gap-5 md:grid-cols-2">
                {obligationFields(catalog, o.kind, o.answers)
                  .filter((f) => f.initial)
                  .map((f) => (
                    <div key={f.key} id={`sw-o${i}-${f.key}`} className={f.type === "select" || f.type === "longText" ? "md:col-span-2" : undefined}>
                      <DynamicField def={f} value={o.answers[f.key]} idPrefix={`o${i}`} error={errors[`o${i}.${f.key}`]}
                        onChange={(v) => {
                          const answers = { ...o.answers };
                          if (v === undefined) delete answers[f.key];
                          else answers[f.key] = v;
                          setOb(o.key, { answers });
                        }} />
                    </div>
                  ))}
              </div>
            </section>
          ))}
          {estimate ? (
            <section aria-labelledby="est-h" className="flex flex-col gap-3 rounded-md border border-info-line bg-info-bg p-4">
              <h3 id="est-h" className="m-0 flex items-center gap-2 text-16 font-bold">
                <Icon name="calculate" size={20} className="text-info" />
                نتيجة أولية تقديرية
              </h3>
              {estimate.ownerAmount !== null ? (
                <p className="m-0 flex flex-wrap items-baseline gap-2 text-15">
                  {d.obligations.some((o) => o.kind === "financier") ? "ما يتبقى لك بعد سداد الجهة وقبل تكاليفك:" : "المبلغ المقترح لك قبل تكاليفك:"}
                  <Amount value={estimate.ownerAmount} size="lg" strong />
                </p>
              ) : null}
              {estimate.gap ? <Alert tone="warn" compact>توجد فجوة: ما يجب سداده أكبر من السعر المطلوب. الأرقام تحتاج مراجعة الفريق.</Alert> : null}
              <p className="m-0 text-13 leading-6 text-charcoal">{estimate.qualityText}</p>
              <p className="m-0 text-13 leading-6 text-muted">هذا تقدير يحتاج مراجعة، وليس عرضًا أو وعدًا بسعر بيع أو باسترداد ما دفعته.</p>
            </section>
          ) : null}
        </div>
      ) : null}

      {d.step === 3 ? (
        <div className="flex flex-col gap-6">
          {!signedIn ? (
            <section className="flex flex-col gap-3 rounded-md border border-line bg-white p-4 md:p-5">
              <h3 className="m-0 text-17 font-bold">ادخل برقم جوالك لحفظ الطلب في حسابك</h3>
              <p className="m-0 text-14 leading-6 text-muted">طلبك محفوظ الآن على هذا الجهاز فقط. بعد الدخول نحفظه في حسابك ونرسله.</p>
              <PhoneSignIn askName initialName={d.name} compact submitLabel="تحقق ومتابعة"
                onSignedIn={async (r) => {
                  if (r.name) update({ name: r.name });
                  setSignedIn(true);
                  dirty.current = true;
                }} />
            </section>
          ) : (
            <>
              <section className="flex flex-col gap-4 rounded-md border border-line bg-white p-4 md:p-5">
                <h3 className="m-0 text-17 font-bold">بيانات التواصل</h3>
                <div id="sw-contactName">
                  <TextField label="الاسم" value={d.name} onChange={(e) => update({ name: e.target.value })} error={errors.contactName} autoComplete="name" requiredMark />
                </div>
                <div id="sw-contactEmail">
                  <TextField label="البريد الإلكتروني (اختياري)" type="email" ltr value={d.email} onChange={(e) => update({ email: e.target.value })} error={errors.contactEmail} autoComplete="email" />
                </div>
                <fieldset id="sw-relationship" className="m-0 flex flex-col gap-2 border-0 p-0">
                  <legend className="mb-2 text-15 font-semibold">علاقتك بالعقار</legend>
                  <Chips name="relationship" options={[{ value: "owner", label: "أنا صاحب العقار" }, { value: "authorized", label: "مخوّل من صاحبه" }]} value={d.relationship}
                    invalid={Boolean(errors.relationship)} onChange={(v) => update({ relationship: v as SellDraft["relationship"] })} />
                  {errors.relationship ? <span className="text-13 text-err">{errors.relationship}</span> : null}
                </fieldset>
              </section>
              <section aria-labelledby="sum-h" className="flex flex-col gap-3 rounded-md border border-line bg-warm p-4 md:p-5">
                <div className="flex items-center justify-between gap-2">
                  <h3 id="sum-h" className="m-0 text-17 font-bold">ملخص طلبك</h3>
                  <Button variant="text" onClick={() => goTo(1)}>تعديل</Button>
                </div>
                <dl className="m-0 grid gap-2 text-15 sm:grid-cols-2">
                  <div><dt className="text-13 text-muted">العقار</dt><dd className="m-0 font-semibold">{label(catalog.propertyTypes, d.propertyType)} · {cityLabel(catalog, d.city)}، {d.district}</dd></div>
                  <div><dt className="text-13 text-muted">الالتزام</dt><dd className="m-0 font-semibold">{d.obligations.map((o) => (o.notInList ? o.partyOther : o.partyName)).join("، ")}</dd></div>
                  {d.obligations.flatMap((o) =>
                    obligationFields(catalog, o.kind, o.answers)
                      .filter((f) => f.initial && isAnswered(o.answers, f.key) && (f.type === "money"))
                      .map((f) => (
                        <div key={`${o.key}-${f.key}`}>
                          <dt className="text-13 text-muted">{f.label}</dt>
                          <dd className="m-0 font-semibold">{o.answers[f.key] === UNKNOWN ? "لا أعرف" : <Amount value={Number(o.answers[f.key])} />}</dd>
                        </div>
                      )),
                  )}
                </dl>
              </section>
              <section className="flex flex-col gap-3">
                <div id="sw-acceptDeclarations">
                  <Checkbox checked={accept.declarations} onChange={(e) => setAccept((a) => ({ ...a, declarations: e.target.checked }))} error={errors.acceptDeclarations}
                    label="أقرّ بأنني صاحب العلاقة بهذا العقار أو مخوّل من صاحبه، وأن ما أدخلته صحيح بحسب علمي." />
                </div>
                <div id="sw-acceptProcessing">
                  <Checkbox checked={accept.processing} onChange={(e) => setAccept((a) => ({ ...a, processing: e.target.checked }))} error={errors.acceptProcessing}
                    label="أوافق على أن يعالج فريق رهون بيانات طلبي ويتواصل معي بشأنه." />
                </div>
              </section>
            </>
          )}
        </div>
      ) : null}

      <div className="sticky bottom-0 z-10 -mx-4 flex items-center justify-between gap-3 border-t border-line bg-white/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0">
        {d.step > 1 ? (
          <Button variant="secondary" onClick={() => goTo((d.step - 1) as 1 | 2)}>
            <Icon name="arrow_forward" size={18} mirror />
            السابق
          </Button>
        ) : (
          <span />
        )}
        {d.step < 3 ? (
          <Button onClick={next}>
            التالي
            <Icon name="arrow_back" size={18} mirror />
          </Button>
        ) : signedIn ? (
          <Button onClick={() => void submit()} loading={submitting} loadingLabel="جارٍ الإرسال…">
            إرسال الطلب
          </Button>
        ) : null}
      </div>
    </div>
  );
}
