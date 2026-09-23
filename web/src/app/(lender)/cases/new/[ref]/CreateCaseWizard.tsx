"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  AmountField,
  Button,
  DateField,
  ErrorSummary,
  Icon,
  IntegrationStateTag,
  RadioCardGroup,
  Select,
  TextField,
  UploadDropzone,
  useToast,
  type ErrorSummaryItem,
} from "@/components/ui";
import { apiSend, apiUpload, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { formatMoney, formatTime } from "@/lib/format";
import { cn } from "@/lib/cn";

/* ───────── Types (GET /api/cases/drafts/{ref}) ───────── */

interface Party {
  id: string;
  role: string;
  kind: string;
  isPrimary: boolean;
  fullName: string;
  nationalIdMasked: string | null;
  hasNationalId: boolean;
  phoneMasked: string | null;
  hasPhone: boolean;
  email: string | null;
  language: string;
  specialNeeds: string | null;
  relation: string | null;
}

interface Duplicate {
  status: "none" | "closed_match" | "open_match";
  caseRef: string | null;
  statusLabel: string | null;
  closedOn: string | null;
}

export interface DraftData {
  reference: string;
  step: number;
  organization: string;
  savedAt: string;
  duplicateOverrideReason: string | null;
  productTypes: string[];
  contract: { contractNumber: string; productType: string; contractDate: string | null; originalAmount: number | null; originalTermMonths: number | null; originalInstallment: number | null; city: string | null } | null;
  parties: Party[];
  property: { type: string; city: string; district: string | null; landAreaM2: number | null; builtAreaM2: number | null; yearBuilt: number | null; deedMasked: string | null; occupancy: string; mortgagee: string; rank: number; registeredOn: string | null } | null;
  debt: { principal: number; profit: number; lateFees: number; otherFees: number; total: number; asOf: string; source: string; arrearsInstallments: number | null; arrearsAmount: number | null; arrearsSince: string | null } | null;
  documents: Array<{ id: string; documentTypeKey: string; name: string; versionCount: number; status: string }>;
  missing: string[];
  duplicate: Duplicate | null;
}

const STEPS = [
  { label: "العقد والمنتج" },
  { label: "المالك والأطراف" },
  { label: "العقار والرهن" },
  { label: "المديونية", sub: "من نظام التمويل" },
  { label: "المستندات الأولية" },
  { label: "المراجعة والإنشاء" },
] as const;

const PROPERTY_TYPES = ["فيلا سكنية", "شقة سكنية", "دور سكني", "دوبلكس", "أرض سكنية", "مبنى تجاري سكني"];
const OCCUPANCY = [
  { value: "OwnerFamily", label: "المالك وأسرته" },
  { value: "Tenant", label: "مستأجر" },
  { value: "Vacant", label: "شاغر" },
  { value: "Unknown", label: "غير معروف" },
];
const NEEDS = ["لا يوجد", "مترجم", "ضعف بصر", "ضعف سمع", "ممثل نظامي"];
const EXTRA_ROLES = [
  { value: "CoBorrower", label: "مقترض مشارك" },
  { value: "Guarantor", label: "كفيل" },
  { value: "Agent", label: "وكيل" },
  { value: "LegalRepresentative", label: "ممثل نظامي" },
];
const INITIAL_DOCS = [
  { key: "title_deed", label: "صك الملكية" },
  { key: "financing_contract", label: "عقد التمويل" },
  { key: "national_id", label: "صورة الهوية الوطنية" },
];

type Errors = Record<string, string[]>;

/** Maps server error keys to field ids and readable summary lines. */
const FIELD_IDS: Record<string, string> = {
  contractNumber: "f-contract",
  productType: "f-product",
  originalAmount: "f-amount",
  originalTermMonths: "f-term",
  contractDate: "f-contract-date",
  "primary.fullName": "f-name",
  "primary.nationalId": "f-id",
  "primary.phone": "f-phone",
  "primary.email": "f-email",
  type: "f-ptype",
  city: "f-city",
  landAreaM2: "f-land",
  yearBuilt: "f-year",
  deedNumber: "f-deed",
  rank: "f-rank",
  principal: "f-principal",
  profit: "f-profit",
  lateFees: "f-late",
  otherFees: "f-other",
  arrearsInstallments: "f-arrears-n",
  arrearsAmount: "f-arrears",
};
const FIELD_LABELS: Record<string, string> = {
  contractNumber: "رقم عقد التمويل",
  productType: "المنتج",
  originalAmount: "مبلغ التمويل",
  originalTermMonths: "مدة التمويل",
  contractDate: "تاريخ العقد",
  "primary.fullName": "الاسم كما في الهوية",
  "primary.nationalId": "رقم الهوية الوطنية",
  "primary.phone": "رقم الجوال",
  "primary.email": "البريد الإلكتروني",
  type: "نوع العقار",
  city: "المدينة",
  deedNumber: "رقم الصك",
  principal: "أصل الدين",
};

export function CreateCaseWizard({ initial }: { initial: DraftData }) {
  const router = useRouter();
  const toast = useToast();
  const submitKey = useIdempotencyKey();
  const [draft, setDraft] = useState(initial);
  const [step, setStep] = useState(Math.min(Math.max(initial.step, 1), 6));
  const [errors, setErrors] = useState<Errors>({});
  const [focusKey, setFocusKey] = useState(0);
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState(initial.savedAt);
  const [dirty, setDirty] = useState(false);
  const [dup, setDup] = useState<Duplicate | null>(initial.duplicate);
  const [overrideReason, setOverrideReason] = useState(initial.duplicateOverrideReason ?? "");
  const [submitError, setSubmitError] = useState<{ title: string; reasons: string[] } | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const primary = draft.parties.find((p) => p.isPrimary);

  // Step state (only the fields of each step; sensitive numbers are never pre-filled).
  const [contract, setContract] = useState({
    contractNumber: initial.contract?.contractNumber ?? "",
    productType: initial.contract?.productType ?? initial.productTypes[0],
    contractDate: initial.contract?.contractDate ?? "",
    originalAmount: initial.contract?.originalAmount ?? null,
    originalTermMonths: initial.contract?.originalTermMonths?.toString() ?? "",
    originalInstallment: initial.contract?.originalInstallment ?? null,
    city: initial.contract?.city ?? "",
  });
  const [owner, setOwner] = useState({
    kind: primary?.kind ?? "Individual",
    fullName: primary?.fullName ?? "",
    nationalId: "",
    phone: "",
    email: primary?.email ?? "",
    language: primary?.language ?? "ar",
    specialNeeds: primary?.specialNeeds ?? "لا يوجد",
  });
  const [extras, setExtras] = useState(
    draft.parties.filter((p) => !p.isPrimary).map((p) => ({ id: p.id as string | null, role: p.role, fullName: p.fullName, nationalId: "", phone: "" })),
  );
  const [property, setProperty] = useState({
    type: initial.property?.type ?? "",
    city: initial.property?.city ?? "",
    district: initial.property?.district ?? "",
    landAreaM2: initial.property?.landAreaM2 ?? null,
    builtAreaM2: initial.property?.builtAreaM2 ?? null,
    yearBuilt: initial.property?.yearBuilt?.toString() ?? "",
    deedNumber: "",
    occupancy: initial.property?.occupancy ?? "Unknown",
    mortgagee: initial.property?.mortgagee ?? initial.organization,
    rank: initial.property?.rank?.toString() ?? "1",
    registeredOn: initial.property?.registeredOn ?? "",
  });
  const [debt, setDebt] = useState({
    principal: initial.debt?.principal ?? null,
    profit: initial.debt?.profit ?? null,
    lateFees: initial.debt?.lateFees ?? null,
    otherFees: initial.debt?.otherFees ?? null,
    source: initial.debt?.source ?? "إدخال يدوي",
    arrearsInstallments: initial.debt?.arrearsInstallments?.toString() ?? "",
    arrearsAmount: initial.debt?.arrearsAmount ?? null,
    arrearsSince: initial.debt?.arrearsSince ?? "",
  });

  const touch = () => setDirty(true);
  const ref = draft.reference;
  const num = (s: string) => (s.trim() === "" ? null : Number(s));

  /** Persists the current step; returns validation errors (the draft is saved regardless). */
  const saveStep = useCallback(
    async (n: number): Promise<Errors> => {
      setSaving(true);
      try {
        let res: { savedAt: string; errors: Errors; duplicate?: Duplicate | null } | null = null;
        if (n === 1)
          res = await apiSend("PUT", `/cases/drafts/${ref}/contract`, {
            ...contract,
            contractDate: contract.contractDate || null,
            originalTermMonths: num(contract.originalTermMonths),
          });
        else if (n === 2)
          res = await apiSend("PUT", `/cases/drafts/${ref}/parties`, {
            primary: { ...owner, nationalId: owner.nationalId || null, phone: owner.phone || null, email: owner.email || null },
            additional: extras.map((x) => ({ id: x.id, role: x.role, kind: "Individual", fullName: x.fullName, nationalId: x.nationalId || null, phone: x.phone || null })),
          });
        else if (n === 3)
          res = await apiSend("PUT", `/cases/drafts/${ref}/property`, {
            ...property,
            yearBuilt: num(property.yearBuilt),
            rank: num(property.rank),
            deedNumber: property.deedNumber || null,
            registeredOn: property.registeredOn || null,
          });
        else if (n === 4)
          res = await apiSend("PUT", `/cases/drafts/${ref}/debt`, {
            ...debt,
            arrearsInstallments: num(debt.arrearsInstallments),
            arrearsSince: debt.arrearsSince || null,
          });
        if (res) {
          setSavedAt(res.savedAt);
          if (res.duplicate !== undefined) setDup(res.duplicate ?? null);
          setDirty(false);
          return res.errors ?? {};
        }
        return {};
      } finally {
        setSaving(false);
      }
    },
    [ref, contract, owner, extras, property, debt],
  );

  // Autosave every 10 s while there are unsaved edits (design: «حفظ تلقائي كل 10 ثوانٍ ومع كل خطوة»).
  const saveRef = useRef(saveStep);
  useEffect(() => {
    saveRef.current = saveStep;
  });
  useEffect(() => {
    if (!dirty || step > 4) return;
    const t = setTimeout(() => void saveRef.current(step).then(setErrors).catch(() => undefined), 10_000);
    return () => clearTimeout(t);
  }, [dirty, step, contract, owner, extras, property, debt]);

  const refresh = async () => setDraft(await apiSend<DraftData>("GET", `/cases/drafts/${ref}`));

  const go = async (next: number) => {
    if (next > step && step <= 4) {
      const errs = await saveStep(step);
      setErrors(errs);
      if (Object.keys(errs).length > 0) {
        setFocusKey((k) => k + 1);
        return;
      }
    } else if (dirty && step <= 4) {
      await saveStep(step).then(setErrors);
    }
    await apiSend("PUT", `/cases/drafts/${ref}/step/${next}`);
    if (next === 6) await refresh();
    setErrors({});
    setStep(next);
    window.scrollTo({ top: 0 });
  };

  const saveAndExit = async () => {
    if (step <= 4) await saveStep(step);
    toast.toast({ tone: "ok", message: `حُفظت المسودة ${ref}` });
    router.push("/cases?view=drafts");
  };

  const submit = async () => {
    setSubmitting(true);
    setSubmitError(null);
    try {
      await apiSend("POST", `/cases/drafts/${ref}/submit`, { duplicateOverrideReason: overrideReason || null }, { idempotencyKey: submitKey.get() });
      submitKey.reset();
      toast.toast({ tone: "ok", message: `أُنشئت الحالة ${ref}` });
      router.push(`/cases/${ref}`);
    } catch (e) {
      submitKey.reset();
      if (isApiError(e)) setSubmitError({ title: e.title, reasons: e.reasons ?? [] });
      else setSubmitError({ title: "تعذّر إنشاء الحالة.", reasons: [] });
    } finally {
      setSubmitting(false);
    }
  };

  const summary: ErrorSummaryItem[] = Object.entries(errors).flatMap(([k, msgs]) =>
    msgs.map((m) => ({ fieldId: FIELD_IDS[k] ?? "wizard-form", message: `${FIELD_LABELS[k] ?? k}: ${m}` })),
  );
  const err = (k: string) => errors[k]?.[0];

  return (
    <div className="-mx-4 -mt-6 flex min-h-[calc(100dvh-64px)] flex-col md:-mx-10 md:-mt-7">
      {/* Mobile header: close + progress (390 frame) */}
      <div className="sticky top-0 z-10 border-b border-line bg-white px-4 py-3 lg:hidden">
        <div className="flex items-center gap-2">
          <Link href="/cases?view=drafts" aria-label="إغلاق وحفظ المسودة" className="grid size-11 place-items-center rounded-sm text-ink" onClick={() => void saveStep(step)}>
            <Icon name="close" size={24} />
          </Link>
          <strong className="flex-1 text-16">حالة جديدة</strong>
          <span className="text-12 text-muted">محفوظة <bdi dir="ltr">{formatTime(savedAt)}</bdi></span>
        </div>
        <div className="mt-2 flex items-center justify-between text-14">
          <strong>{STEPS[step - 1].label}</strong>
          <span className="text-muted">{step} من 6</span>
        </div>
        <div className="mt-1.5 grid grid-cols-6 gap-1" aria-hidden="true">
          {STEPS.map((_, i) => (
            <span key={i} className={cn("h-1.5 rounded-full", i + 1 < step ? "bg-charcoal" : i + 1 === step ? "bg-orange" : "bg-track")} />
          ))}
        </div>
      </div>

      <div className="grid flex-1 gap-6 px-4 py-6 md:px-10 lg:grid-cols-[240px_minmax(0,1fr)_300px]">
        {/* Steps (desktop) */}
        <nav aria-label="خطوات الإنشاء" className="hidden flex-col gap-3 lg:flex">
          <h1 className="m-0 text-24 font-bold">حالة جديدة</h1>
          <p className="m-0 text-13 text-muted">
            <bdi dir="ltr" className="font-mono">{ref}</bdi>
          </p>
          <ol className="m-0 flex list-none flex-col gap-1 p-0">
            {STEPS.map((s, i) => {
              const n = i + 1;
              const state = n < step ? "done" : n === step ? "cur" : "todo";
              return (
                <li key={s.label} aria-current={state === "cur" ? "step" : undefined}>
                  <button
                    type="button"
                    onClick={() => void go(n)}
                    className={cn("flex w-full items-center gap-3 rounded-sm px-2 py-2 text-start", state === "cur" && "bg-white shadow-1")}
                  >
                    <span
                      className={cn(
                        "grid size-[26px] shrink-0 place-items-center rounded-full text-13 font-semibold",
                        state === "done" && "bg-charcoal text-white",
                        state === "cur" && "bg-orange text-white",
                        state === "todo" && "border border-line-strong bg-white text-muted",
                      )}
                    >
                      {state === "done" ? <Icon name="check" size={16} /> : n}
                    </span>
                    <span className="flex flex-col">
                      <span className={cn("text-14", state === "cur" ? "font-bold" : "font-medium")}>{s.label}</span>
                      {n === step && summary.length > 0 ? (
                        <span className="text-12 text-err">{summary.length === 1 ? "خطأ واحد" : summary.length === 2 ? "خطآن" : `${summary.length} أخطاء`}</span>
                      ) : "sub" in s ? (
                        <span className="text-12 text-muted">{s.sub}</span>
                      ) : null}
                    </span>
                  </button>
                </li>
              );
            })}
          </ol>
          <p className="m-0 flex items-center gap-1.5 text-13 text-muted" role="status">
            <Icon name={saving ? "sync" : "cloud_done"} size={18} />
            {saving ? "جارٍ الحفظ…" : <>حُفظت المسودة تلقائياً <bdi dir="ltr">{formatTime(savedAt)}</bdi></>}
          </p>
        </nav>

        {/* Form */}
        <form
          id="wizard-form"
          className="flex min-w-0 flex-col gap-5"
          onSubmit={(e) => {
            e.preventDefault();
            if (step < 6) void go(step + 1);
          }}
          onChange={touch}
        >
          <div className="flex flex-col gap-1">
            <span className="text-13 font-semibold text-muted">الخطوة {step} من 6</span>
            <h2 className="m-0 text-24 font-bold">{STEPS[step - 1].label}</h2>
            {step === 2 ? <p className="m-0 text-14 text-muted">لن يصل أي تواصل للمالك قبل اكتمال التحقق وإرسال الدعوة يدوياً.</p> : null}
          </div>

          {summary.length > 0 ? (
            <ErrorSummary errors={summary} focusKey={focusKey} title={summary.length === 1 ? "يوجد خطأ واحد في هذه الخطوة" : summary.length === 2 ? "يوجد خطآن في هذه الخطوة" : `يوجد ${summary.length} أخطاء في هذه الخطوة`} />
          ) : null}

          {step === 1 ? (
            <section className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
              <div className="grid gap-4 md:grid-cols-2">
                <TextField id="f-contract" label="رقم عقد التمويل" requiredMark ltr mono value={contract.contractNumber} error={err("contractNumber")}
                  help="يُفحص التكرار عند الخروج من الحقل"
                  onChange={(e) => setContract({ ...contract, contractNumber: e.target.value.toUpperCase() })}
                  onBlur={async () => {
                    if (contract.contractNumber.trim().length >= 6)
                      setDup(await apiSend<Duplicate>("GET", `/cases/duplicate-check?contract=${encodeURIComponent(contract.contractNumber)}&exclude=${ref}`));
                  }} />
                <Select id="f-product" label="المنتج" requiredMark value={contract.productType} error={err("productType")}
                  onChange={(e) => setContract({ ...contract, productType: e.target.value })}
                  options={draft.productTypes.map((p) => ({ value: p, label: p }))} />
                <DateField id="f-contract-date" label="تاريخ العقد" value={contract.contractDate} error={err("contractDate")} onValueChange={(v) => { setContract({ ...contract, contractDate: v }); touch(); }} />
                <AmountField id="f-amount" label="مبلغ التمويل الأصلي" value={contract.originalAmount} error={err("originalAmount")} onValueChange={(v) => { setContract({ ...contract, originalAmount: v }); touch(); }} />
                <TextField id="f-term" label="مدة التمويل (شهراً)" ltr inputMode="numeric" value={contract.originalTermMonths} error={err("originalTermMonths")}
                  onChange={(e) => setContract({ ...contract, originalTermMonths: e.target.value.replace(/\D/g, "") })} />
                <AmountField id="f-installment" label="القسط الأصلي" value={contract.originalInstallment} onValueChange={(v) => { setContract({ ...contract, originalInstallment: v }); touch(); }} />
                <TextField id="f-city-c" label="مدينة الفرع" value={contract.city} onChange={(e) => setContract({ ...contract, city: e.target.value })} />
              </div>
              <DuplicateNotice dup={dup} reason={overrideReason} onReason={setOverrideReason} />
            </section>
          ) : null}

          {step === 2 ? (
            <>
              <section className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
                <div className="flex items-center justify-between">
                  <strong className="text-16">المالك الأساسي (المقترض)</strong>
                  <span className="text-13 text-muted">* إلزامي</span>
                </div>
                <RadioCardGroup legend="نوع الطرف *" name="kind" columns={2} value={owner.kind}
                  onChange={(v) => { setOwner({ ...owner, kind: v }); touch(); }}
                  options={[{ value: "Individual", label: "فرد" }, { value: "Organization", label: "منشأة" }]} />
                <div className="grid gap-4 md:grid-cols-2">
                  <TextField id="f-name" label={owner.kind === "Organization" ? "اسم المنشأة كما في السجل" : "الاسم كما في الهوية"} requiredMark value={owner.fullName} error={err("primary.fullName")}
                    onChange={(e) => setOwner({ ...owner, fullName: e.target.value })} />
                  <TextField id="f-id" label={owner.kind === "Organization" ? "رقم السجل الموحد" : "رقم الهوية الوطنية"} requiredMark ltr mono inputMode="numeric" autoComplete="off"
                    value={owner.nationalId} error={err("primary.nationalId")} maxLength={10}
                    placeholder={primary?.hasNationalId ? `محفوظ: ${primary.nationalIdMasked}` : undefined}
                    help={primary?.hasNationalId ? "محفوظ مشفراً ويُعرض مخفياً؛ اتركه فارغاً للإبقاء عليه." : "10 أرقام"}
                    onChange={(e) => setOwner({ ...owner, nationalId: e.target.value.replace(/\D/g, "") })} />
                  <TextField id="f-phone" label="الجوال" requiredMark ltr mono inputMode="tel" autoComplete="off" prefix={<bdi dir="ltr">+966</bdi>}
                    value={owner.phone} error={err("primary.phone")}
                    placeholder={primary?.hasPhone ? `محفوظ: ${primary.phoneMasked}` : "05XXXXXXXX"}
                    onChange={(e) => setOwner({ ...owner, phone: e.target.value.replace(/[^\d ]/g, "") })} />
                  <TextField id="f-email" label="البريد الإلكتروني" optionalMark ltr type="email" placeholder="name@example.sa" value={owner.email} error={err("primary.email")}
                    onChange={(e) => setOwner({ ...owner, email: e.target.value })} />
                  <Select label="لغة التواصل المفضلة" value={owner.language} onChange={(e) => setOwner({ ...owner, language: e.target.value })}
                    options={[{ value: "ar", label: "العربية" }, { value: "en", label: "English" }]} />
                  <Select label="احتياجات تواصل خاصة" value={owner.specialNeeds} help="مثل: مترجم، ضعف بصر، ممثل نظامي"
                    onChange={(e) => setOwner({ ...owner, specialNeeds: e.target.value })} options={NEEDS.map((n) => ({ value: n, label: n }))} />
                </div>
              </section>

              <section className="flex flex-col gap-3 rounded-lg border border-dashed border-line-strong bg-warm p-5">
                <div className="flex items-center gap-3">
                  <Icon name="group_add" size={24} />
                  <div className="flex flex-1 flex-col">
                    <strong>أطراف إضافية</strong>
                    <span className="text-13 text-muted">مقترض مشارك، كفيل، وكيل أو ممثل نظامي</span>
                  </div>
                  <Button variant="secondary" type="button" onClick={() => { setExtras([...extras, { id: null, role: "CoBorrower", fullName: "", nationalId: "", phone: "" }]); touch(); }}>
                    إضافة طرف
                  </Button>
                </div>
                {extras.map((x, i) => (
                  <div key={x.id ?? `new-${i}`} className="grid gap-3 rounded-md border border-line bg-white p-4 md:grid-cols-[1fr_1.4fr_1fr_1fr_auto] md:items-end">
                    <Select label="الصفة" value={x.role} options={EXTRA_ROLES} onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, role: e.target.value } : y)))} />
                    <TextField label="الاسم" value={x.fullName} error={errors[`additional[${i}].fullName`]?.[0]} onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, fullName: e.target.value } : y)))} />
                    <TextField label="رقم الهوية" optionalMark ltr mono inputMode="numeric" value={x.nationalId} error={errors[`additional[${i}].nationalId`]?.[0]}
                      onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, nationalId: e.target.value.replace(/\D/g, "") } : y)))} />
                    <TextField label="الجوال" optionalMark ltr mono value={x.phone} error={errors[`additional[${i}].phone`]?.[0]}
                      onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, phone: e.target.value } : y)))} />
                    <Button variant="text" type="button" onClick={() => { setExtras(extras.filter((_, j) => j !== i)); touch(); }}>حذف</Button>
                  </div>
                ))}
              </section>
            </>
          ) : null}

          {step === 3 ? (
            <section className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
              <div className="grid gap-4 md:grid-cols-2">
                <Select id="f-ptype" label="نوع العقار" requiredMark value={property.type} placeholder="اختر" error={err("type")}
                  onChange={(e) => setProperty({ ...property, type: e.target.value })} options={PROPERTY_TYPES.map((p) => ({ value: p, label: p }))} />
                <TextField id="f-city" label="المدينة" requiredMark value={property.city} error={err("city")} onChange={(e) => setProperty({ ...property, city: e.target.value })} />
                <TextField label="الحي" value={property.district} placeholder="حي النرجس" onChange={(e) => setProperty({ ...property, district: e.target.value })} />
                <TextField id="f-deed" label="رقم الصك" ltr mono inputMode="numeric" value={property.deedNumber} error={err("deedNumber")}
                  placeholder={initial.property?.deedMasked ? `محفوظ: ${initial.property.deedMasked}` : undefined}
                  help="يُخزن مشفراً ويُعرض مخفياً" onChange={(e) => setProperty({ ...property, deedNumber: e.target.value.replace(/\D/g, "") })} />
                <TextField id="f-land" label="مساحة الأرض" ltr inputMode="decimal" endAdornment={<span className="px-3 text-muted">م²</span>}
                  value={property.landAreaM2?.toString() ?? ""} error={err("landAreaM2")}
                  onChange={(e) => setProperty({ ...property, landAreaM2: e.target.value === "" ? null : Number(e.target.value.replace(/[^\d.]/g, "")) })} />
                <TextField label="مسطح البناء" ltr inputMode="decimal" endAdornment={<span className="px-3 text-muted">م²</span>}
                  value={property.builtAreaM2?.toString() ?? ""}
                  onChange={(e) => setProperty({ ...property, builtAreaM2: e.target.value === "" ? null : Number(e.target.value.replace(/[^\d.]/g, "")) })} />
                <TextField id="f-year" label="سنة البناء" ltr inputMode="numeric" value={property.yearBuilt} error={err("yearBuilt")} onChange={(e) => setProperty({ ...property, yearBuilt: e.target.value.replace(/\D/g, "") })} />
                <Select label="الإشغال" value={property.occupancy} options={OCCUPANCY} onChange={(e) => setProperty({ ...property, occupancy: e.target.value })} />
              </div>
              <h3 className="m-0 mt-2 text-16 font-semibold">الرهن</h3>
              <div className="grid gap-4 md:grid-cols-3">
                <TextField label="المرتهن" value={property.mortgagee} onChange={(e) => setProperty({ ...property, mortgagee: e.target.value })} />
                <TextField id="f-rank" label="درجة الرهن" ltr inputMode="numeric" value={property.rank} error={err("rank")} onChange={(e) => setProperty({ ...property, rank: e.target.value.replace(/\D/g, "") })} />
                <DateField label="تاريخ تسجيل الرهن" value={property.registeredOn} onValueChange={(v) => { setProperty({ ...property, registeredOn: v }); touch(); }} />
              </div>
              <Alert tone="info" title="مطابقة الصك مع السجل العقاري يدوية" body="لا يوجد تكامل مع السجل العقاري؛ تراجع القانونية الصك والرهن في مرحلة التحقق." />
            </section>
          ) : null}

          {step === 4 ? (
            <section className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
              <div className="flex flex-wrap items-center gap-2 text-14">
                <span>نظام التمويل الأساسي:</span>
                <IntegrationStateTag state="unavailable" />
                <span className="text-muted">تُدخل الأرقام يدوياً مع ذكر المصدر ووقته.</span>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <AmountField id="f-principal" label="أصل الدين المتبقي" requiredMark value={debt.principal} error={err("principal")} onValueChange={(v) => { setDebt({ ...debt, principal: v }); touch(); }} />
                <AmountField id="f-profit" label="الأرباح المستحقة" value={debt.profit} error={err("profit")} onValueChange={(v) => { setDebt({ ...debt, profit: v }); touch(); }} />
                <AmountField id="f-late" label="غرامات التأخير" value={debt.lateFees} error={err("lateFees")} onValueChange={(v) => { setDebt({ ...debt, lateFees: v }); touch(); }} />
                <AmountField id="f-other" label="رسوم أخرى" value={debt.otherFees} error={err("otherFees")} onValueChange={(v) => { setDebt({ ...debt, otherFees: v }); touch(); }} />
              </div>
              <div className="flex items-baseline justify-between rounded-md bg-warm px-4 py-3">
                <span className="text-14 font-semibold">إجمالي المديونية القائمة</span>
                <span className="text-20 font-bold tabular-nums">
                  <bdi dir="ltr">{formatMoney((debt.principal ?? 0) + (debt.profit ?? 0) + (debt.lateFees ?? 0) + (debt.otherFees ?? 0))}</bdi> ر.س
                </span>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <TextField label="المصدر" value={debt.source} help="مثل: كشف نظام التمويل بتاريخ اليوم" onChange={(e) => setDebt({ ...debt, source: e.target.value })} />
                <TextField id="f-arrears-n" label="عدد الأقساط المتأخرة" ltr inputMode="numeric" value={debt.arrearsInstallments} error={err("arrearsInstallments")} onChange={(e) => setDebt({ ...debt, arrearsInstallments: e.target.value.replace(/\D/g, "") })} />
                <AmountField id="f-arrears" label="مبلغ المتأخرات" value={debt.arrearsAmount} error={err("arrearsAmount")} onValueChange={(v) => { setDebt({ ...debt, arrearsAmount: v }); touch(); }} />
                <DateField label="متأخر منذ" value={debt.arrearsSince} onValueChange={(v) => { setDebt({ ...debt, arrearsSince: v }); touch(); }} />
              </div>
            </section>
          ) : null}

          {step === 5 ? <DocumentsStep reference={ref} docs={draft.documents} onUploaded={refresh} /> : null}

          {step === 6 ? (
            <ReviewStep draft={draft} dup={dup} overrideReason={overrideReason} onReason={setOverrideReason} submitError={submitError} />
          ) : null}
        </form>

        {/* Summary aside */}
        <aside aria-label="ملخص الحالة" className="hidden flex-col gap-3 lg:flex">
          <div className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
            <strong>ما أُدخل حتى الآن</strong>
            <Row k="المنشأة" v={draft.organization} />
            <Row k="العقد" v={contract.contractNumber ? <bdi dir="ltr" className="font-mono">{contract.contractNumber.length > 10 ? `${contract.contractNumber.slice(0, 10)}•••` : contract.contractNumber}</bdi> : "—"} />
            <Row k="المنتج" v={contract.productType} />
            <Row k="المالك" v={owner.fullName ? owner.fullName.split(" ")[0] + (owner.fullName.split(" ")[1] ? ` ${owner.fullName.split(" ")[1][0]}.` : "") : "—"} />
            <Row k="المصدر" v="إدخال يدوي" />
          </div>
          {dup?.status === "none" ? (
            <div className="flex gap-2 rounded-lg border border-ok-line bg-ok-bg p-4 text-14 text-ok">
              <Icon name="task_alt" size={20} />
              <span><strong>لا تكرار</strong><br />العقد غير مرتبط بأي حالة مفتوحة في منشأتك.</span>
            </div>
          ) : null}
          <div className="flex gap-2 rounded-lg bg-subtle p-4 text-13 text-muted">
            <Icon name="shield" size={20} />
            <span>بيانات الهوية تُخزن مشفرة وتُعرض مخفية لكل الأدوار بعد الحفظ.</span>
          </div>
        </aside>
      </div>

      {/* Footer actions (sticky; 48px on mobile) */}
      <div className="sticky bottom-0 z-10 flex flex-wrap items-center gap-3 border-t border-line bg-white px-4 py-3 md:px-10">
        <Button variant="secondary" icon="arrow_forward" disabled={step === 1} onClick={() => void go(step - 1)} className="max-md:min-h-12">
          السابق
        </Button>
        <Button variant="text" onClick={() => void saveAndExit()} className="max-md:hidden">حفظ والخروج</Button>
        <span className="ms-auto text-13 text-muted max-md:hidden" aria-live="polite">
          {summary.length > 0 ? (summary.length === 2 ? "صحّح الخطأين للمتابعة" : "صحّح الأخطاء للمتابعة") : null}
        </span>
        {step < 6 ? (
          <Button type="submit" form="wizard-form" iconEnd="arrow_back" loading={saving} className="max-md:ms-auto max-md:min-h-12">
            التالي{step < 5 ? `: ${STEPS[step].label}` : ""}
          </Button>
        ) : (
          <Button onClick={() => void submit()} loading={submitting} disabled={draft.missing.length > 0 || (dup?.status === "open_match" && overrideReason.trim().length < 10)}
            className="max-md:ms-auto max-md:min-h-12">
            إنشاء الحالة
          </Button>
        )}
      </div>
    </div>
  );
}

function Row({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted">{k}</span>
      <span className="text-end font-medium">{v}</span>
    </div>
  );
}

function DuplicateNotice({ dup, reason, onReason }: { dup: Duplicate | null; reason: string; onReason: (s: string) => void }) {
  if (!dup || dup.status === "none") return null;
  const open = dup.status === "open_match";
  return (
    <div role="alert" className={cn("flex flex-col gap-3 rounded-md border p-4 text-14", open ? "border-err-line bg-err-bg" : "border-warn-line bg-warn-bg")}>
      <div className="flex gap-2">
        <Icon name="content_copy" size={20} />
        <span>
          <strong>{open ? "العقد مرتبط بحالة مفتوحة:" : "تنبيه تكرار محتمل:"}</strong> العقد مرتبط بالحالة{" "}
          <Link href={`/cases/${dup.caseRef}`} target="_blank"><bdi dir="ltr">{dup.caseRef}</bdi></Link> ({dup.statusLabel}
          {dup.closedOn ? <> <bdi dir="ltr">{dup.closedOn}</bdi></> : null}).
        </span>
      </div>
      {open ? (
        <TextField label="سبب المتابعة بحالة جديدة" requiredMark value={reason} help="يُسجَّل في سجل الحالة، ويراه المعتمد." onChange={(e) => onReason(e.target.value)} />
      ) : null}
    </div>
  );
}

function DocumentsStep({ reference, docs, onUploaded }: { reference: string; docs: DraftData["documents"]; onUploaded: () => Promise<void> }) {
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const upload = async (key: string, file: File) => {
    setBusy(key);
    setError(null);
    try {
      const fd = new FormData();
      fd.append("file", file);
      fd.append("documentTypeKey", key);
      await apiUpload(`/cases/${reference}/documents`, fd);
      await onUploaded();
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر رفع الملف.");
    } finally {
      setBusy(null);
    }
  };
  return (
    <section className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <p className="m-0 text-14 text-muted">اختياري عند الإنشاء: يمكن رفع المستندات لاحقاً من تبويب المستندات. كل ملف يُفحص قبل المراجعة، والإصدارات لا تُحذف.</p>
      {error ? <Alert tone="err" title={error} /> : null}
      {INITIAL_DOCS.map((d) => {
        const existing = docs.find((x) => x.documentTypeKey === d.key);
        return (
          <div key={d.key} className="flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <strong className="text-15">{d.label}</strong>
              {existing ? <span className="text-13 text-ok">مرفوع · v{existing.versionCount} · قيد المراجعة</span> : <span className="text-13 text-muted">لم يُرفع</span>}
            </div>
            <UploadDropzone disabled={busy !== null} onFiles={(files) => files[0] && void upload(d.key, files[0])} hint={busy === d.key ? "جارٍ الرفع والفحص…" : "PDF أو JPG أو PNG · حتى 20 م.ب"} />
          </div>
        );
      })}
    </section>
  );
}

function ReviewStep({ draft, dup, overrideReason, onReason, submitError }: {
  draft: DraftData;
  dup: Duplicate | null;
  overrideReason: string;
  onReason: (s: string) => void;
  submitError: { title: string; reasons: string[] } | null;
}) {
  const p = draft.parties.find((x) => x.isPrimary);
  return (
    <div className="flex flex-col gap-4">
      {submitError ? (
        <Alert tone="err" title={submitError.title} body={submitError.reasons.length ? <ul className="m-0 ps-5">{submitError.reasons.map((r) => <li key={r}>{r}</li>)}</ul> : undefined} />
      ) : null}
      {draft.missing.length > 0 ? (
        <Alert tone="warn" title="بيانات إلزامية ناقصة" body={<ul className="m-0 ps-5">{draft.missing.map((m) => <li key={m}>{m}</li>)}</ul>} />
      ) : (
        <Alert tone="ok" title="كل البيانات الإلزامية مكتملة" body="عند الإنشاء تنتقل الحالة إلى «بانتظار البيانات» ويبدأ احتساب مهلة المرحلة. لن يُتواصل مع المالك قبل إرسال الدعوة يدوياً." />
      )}
      <DuplicateNotice dup={dup} reason={overrideReason} onReason={onReason} />
      <div className="grid gap-4 md:grid-cols-2">
        <SummaryCard title="العقد والمنتج" rows={[
          ["رقم العقد", draft.contract?.contractNumber ?? "—"],
          ["المنتج", draft.contract?.productType ?? "—"],
          ["مبلغ التمويل", draft.contract?.originalAmount != null ? `${formatMoney(draft.contract.originalAmount)} ر.س` : "—"],
        ]} />
        <SummaryCard title="المالك والأطراف" rows={[
          ["المالك", p?.fullName ?? "—"],
          ["الهوية", p?.nationalIdMasked ?? "—"],
          ["الجوال", p?.phoneMasked ?? "—"],
          ["أطراف إضافية", String(draft.parties.filter((x) => !x.isPrimary).length)],
        ]} />
        <SummaryCard title="العقار والرهن" rows={[
          ["العقار", draft.property ? `${draft.property.type}، ${draft.property.district ?? ""} ${draft.property.city}` : "—"],
          ["الصك", draft.property?.deedMasked ?? "—"],
          ["المرتهن", draft.property ? `${draft.property.mortgagee} · الدرجة ${draft.property.rank}` : "—"],
        ]} />
        <SummaryCard title="المديونية" rows={[
          ["الإجمالي", draft.debt ? `${formatMoney(draft.debt.total)} ر.س` : "—"],
          ["المصدر", draft.debt?.source ?? "—"],
          ["الأقساط المتأخرة", draft.debt?.arrearsInstallments?.toString() ?? "—"],
        ]} />
      </div>
    </div>
  );
}

function SummaryCard({ title, rows }: { title: string; rows: Array<[string, string]> }) {
  return (
    <section className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
      <h3 className="m-0 text-16 font-semibold">{title}</h3>
      {rows.map(([k, v]) => (
        <div key={k} className="flex justify-between gap-3">
          <span className="text-muted">{k}</span>
          <span className="text-end font-medium">{/^[A-Z0-9•+ -]+$/.test(v) ? <bdi dir="ltr">{v}</bdi> : v}</span>
        </div>
      ))}
    </section>
  );
}
