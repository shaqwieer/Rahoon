"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import type { ArrearsKey, DocKindKey, PreferenceKey } from "@/components/individual/copy";
import { IndividualFrame, Panel, requestErrorText, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { ChoiceCard, SegmentProgress } from "@/components/owner/ui";
import { Alert, SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { AmountField, Checkbox, Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { OtpInput } from "@/components/ui/OtpInput";
import { Tag } from "@/components/ui/Status";
import { apiSend, apiUpload, isApiError } from "@/lib/api/client";
import type { Institution, MyRequestDetail } from "@/lib/api/requests";
import { cn } from "@/lib/cn";
import { formatDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

type StepKey = "lender" | "finance" | "situation";
type SaveState = "idle" | "saving" | "saved" | "error";

interface Fields {
  institutionId: string | null;
  useOther: boolean;
  institutionOther: string;
  applicantFullName: string;
  contractNumber: string;
  monthlyInstallment: number | null;
  arrearsDuration: ArrearsKey | null;
  propertyCity: string;
  pathPreference: PreferenceKey | null;
  affordableMonthly: number | null;
  situationText: string;
}

const ARREARS: ArrearsKey[] = ["not_late", "lt3m", "3to6m", "6to12m", "gt12m"];
const PREFERENCES: PreferenceKey[] = ["keep_home", "settlement", "sell_myself", "not_sure"];
const DOC_KINDS: DocKindKey[] = ["salary_statement", "bank_statement", "title_deed", "financing_contract"];

function fromDetail(d: MyRequestDetail): Fields {
  const f = d.fields;
  return {
    institutionId: d.institution?.id ?? null,
    useOther: !d.institution && Boolean(d.institutionOtherName),
    institutionOther: d.institutionOtherName ?? "",
    applicantFullName: f.applicantFullName ?? "",
    contractNumber: f.contractNumber ?? "",
    monthlyInstallment: f.monthlyInstallment,
    arrearsDuration: f.arrearsDuration,
    propertyCity: f.propertyCity ?? "",
    pathPreference: f.pathPreference,
    affordableMonthly: f.affordableMonthly,
    situationText: f.situationText ?? "",
  };
}

function patchBody(step: StepKey, v: Fields) {
  switch (step) {
    case "lender":
      return { step, institutionId: v.useOther ? null : v.institutionId, institutionOtherName: v.useOther ? v.institutionOther.trim() || null : null };
    case "finance":
      return {
        step,
        applicantFullName: v.applicantFullName.trim() || null,
        contractNumber: v.contractNumber.trim() || null,
        monthlyInstallment: v.monthlyInstallment,
        arrearsDuration: v.arrearsDuration,
        propertyCity: v.propertyCity.trim() || null,
      };
    case "situation":
      return { step, pathPreference: v.pathPreference, affordableMonthly: v.affordableMonthly, situationText: v.situationText.trim() || null };
  }
}

const STEP_KEYS: Record<number, StepKey | null> = { 1: "lender", 2: "finance", 3: "situation", 4: null, 5: null };

/**
 * OA01–OA05. Each step autosaves (debounced, serialized so concurrent saves never race) and «التالي» flushes before
 * moving on. The server re-reads the draft on every step, so consent and documents shown here are always current.
 */
export function RequestWizard({ initial, institutions, step }: { initial: MyRequestDetail; institutions: Institution[]; step: number }) {
  const c = useRequestCopy();
  const W = c.wizard;
  const router = useRouter();
  const ref = initial.reference;
  const detail = initial;
  const [f, setF] = useState<Fields>(() => fromDetail(initial));
  const [save, setSave] = useState<SaveState>("idle");
  const [errors, setErrors] = useState<Record<string, string>>({});
  // The wizard instance survives step changes (same key), so «busy» belongs to the step that started it.
  const [movingFrom, setMovingFrom] = useState<number | null>(null);
  const moving = movingFrom === step;
  const latest = useRef<Fields>(f);
  const chain = useRef<Promise<void>>(Promise.resolve());
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const stepKey = STEP_KEYS[step];

  const flush = (s: StepKey): Promise<void> => {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    const body = patchBody(s, latest.current);
    const run = async () => {
      setSave("saving");
      try {
        await apiSend("PATCH", `/my/requests/${encodeURIComponent(ref)}`, body);
        setSave("saved");
      } catch (e) {
        setSave("error");
        throw e;
      }
    };
    const p = chain.current.then(run, run);
    chain.current = p.catch(() => undefined);
    return p;
  };

  const update = (patch: Partial<Fields>) => {
    const next = { ...latest.current, ...patch };
    latest.current = next;
    setF(next);
    setErrors((e) => {
      const copy = { ...e };
      for (const k of Object.keys(patch)) delete copy[k];
      return copy;
    });
    if (!stepKey) return;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void flush(stepKey).catch(() => undefined), 700);
  };

  const go = (n: number) => router.push(`/my/requests/${encodeURIComponent(ref)}/apply?step=${n}`);

  const validate = (): Record<string, string> => {
    const e: Record<string, string> = {};
    if (step === 1) {
      if (f.useOther && !f.institutionOther.trim()) e.institutionOther = W.lender.otherError;
      if (!f.useOther && !f.institutionId) e.institution = W.lender.pickError;
    }
    if (step === 2) {
      if (!f.applicantFullName.trim()) e.applicantFullName = W.finance.nameError;
      if (f.monthlyInstallment === null) e.monthlyInstallment = W.finance.installmentError;
      if (!f.arrearsDuration) e.arrearsDuration = W.finance.arrearsError;
      if (!f.propertyCity.trim()) e.propertyCity = W.finance.cityError;
    }
    if (step === 3 && !f.pathPreference) e.pathPreference = W.situation.preferenceError;
    return e;
  };

  const next = async (ev?: FormEvent) => {
    ev?.preventDefault();
    const e = validate();
    setErrors(e);
    if (Object.keys(e).length > 0) {
      document.getElementById(`field-${Object.keys(e)[0]}`)?.focus();
      return;
    }
    setMovingFrom(step);
    try {
      if (stepKey) await flush(stepKey);
      go(step + 1);
    } catch (err) {
      setErrors({ form: requestErrorText(err, c) });
      setMovingFrom(null);
    }
  };

  const back = () => {
    if (stepKey) void flush(stepKey).catch(() => undefined);
    if (step === 1) router.push("/my");
    else go(step - 1);
  };

  const saveLabel = save === "saving" ? c.saving : save === "saved" ? c.saved : save === "error" ? c.saveFailed : null;
  const sub = (
    <>
      {W.stepOf(step)} · {W.stepNames[step - 1]}
      {saveLabel ? <span className={cn(save === "error" && "text-err")}> · {saveLabel}</span> : null}
    </>
  );

  return (
    <IndividualFrame title={W.title} sub={sub} back={{ onClick: back }}>
      <SegmentProgress current={step} total={5} label={W.progress(step)} />
      <span className="sr-only" aria-live="polite">
        {saveLabel}
      </span>
      {errors.form ? <Alert tone="err">{errors.form}</Alert> : null}
      {step === 1 ? <LenderStep f={f} update={update} errors={errors} institutions={institutions} /> : null}
      {step === 2 ? <FinanceStep f={f} update={update} errors={errors} /> : null}
      {step === 3 ? <SituationStep f={f} update={update} errors={errors} /> : null}
      {step === 4 ? <DocsConsentStep detail={detail} /> : null}
      {step === 5 ? <ReviewStep detail={detail} institutions={institutions} /> : null}
      {step < 5 ? (
        <Button size="xl" fullWidth loading={moving} onClick={() => void next()} className="mt-2">
          {c.next}
        </Button>
      ) : null}
    </IndividualFrame>
  );
}

/* ───────── OA01 — lender ───────── */

function LenderStep({ f, update, errors, institutions }: { f: Fields; update: (p: Partial<Fields>) => void; errors: Record<string, string>; institutions: Institution[] }) {
  const c = useRequestCopy();
  const L = c.wizard.lender;
  const { locale } = useI18n();
  const [q, setQ] = useState("");
  const shown = useMemo(() => {
    const s = q.trim().toLowerCase();
    return s ? institutions.filter((i) => i.nameAr.includes(q.trim()) || (i.nameEn ?? "").toLowerCase().includes(s)) : institutions;
  }, [q, institutions]);
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 leading-9 font-bold">{L.heading}</h1>
      <p className="m-0 text-17 leading-7 text-charcoal">{L.lead}</p>
      <TextField label={L.search} size="lg" value={q} onChange={(e) => setQ(e.target.value)} type="search" autoComplete="off" />
      <fieldset id="field-institution" tabIndex={-1} className="m-0 flex flex-col gap-2 border-0 p-0 outline-none" aria-describedby={errors.institution ? "institution-error" : undefined}>
        <legend className="sr-only">{L.heading}</legend>
        {shown.map((i) => (
          <ChoiceCard
            key={i.id}
            type="radio"
            name="institution"
            checked={!f.useOther && f.institutionId === i.id}
            onChange={() => update({ institutionId: i.id, useOther: false })}
            label={
              <span className="flex flex-col">
                <span>{locale === "en" && i.nameEn ? i.nameEn : i.nameAr}</span>
                <span className="text-13 font-normal text-muted">{i.kind === "bank" ? L.bank : L.financeCompany}</span>
              </span>
            }
          />
        ))}
        {shown.length === 0 ? <p className="m-0 text-15 text-muted">{L.noMatch}</p> : null}
        <ChoiceCard type="radio" name="institution" checked={f.useOther} onChange={() => update({ useOther: true, institutionId: null })} label={L.other} />
        {errors.institution ? (
          <span id="institution-error" className="flex items-center gap-1 text-14 text-err">
            <Icon name="error" size={16} />
            {errors.institution}
          </span>
        ) : null}
      </fieldset>
      {f.useOther ? (
        <TextField
          id="field-institutionOther"
          label={L.otherLabel}
          size="lg"
          value={f.institutionOther}
          maxLength={200}
          onChange={(e) => update({ institutionOther: e.target.value })}
          error={errors.institutionOther}
        />
      ) : null}
      <p className="m-0 flex items-start gap-1.5 text-14 text-muted">
        <Icon name="info" size={18} />
        {L.hint}
      </p>
    </div>
  );
}

/* ───────── OA02 — finance and property ───────── */

function FinanceStep({ f, update, errors }: { f: Fields; update: (p: Partial<Fields>) => void; errors: Record<string, string> }) {
  const c = useRequestCopy();
  const F = c.wizard.finance;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 leading-9 font-bold">{F.heading}</h1>
      <TextField
        id="field-applicantFullName"
        label={F.name}
        size="lg"
        autoComplete="name"
        maxLength={200}
        value={f.applicantFullName}
        onChange={(e) => update({ applicantFullName: e.target.value })}
        error={errors.applicantFullName}
      />
      <TextField
        id="field-contractNumber"
        label={F.contract}
        optionalMark
        help={F.contractHint}
        size="lg"
        ltr
        mono
        maxLength={60}
        value={f.contractNumber}
        onChange={(e) => update({ contractNumber: e.target.value })}
      />
      <AmountField
        id="field-monthlyInstallment"
        label={F.installment}
        size="lg"
        value={f.monthlyInstallment}
        onValueChange={(v) => update({ monthlyInstallment: v })}
        error={errors.monthlyInstallment}
      />
      <fieldset id="field-arrearsDuration" tabIndex={-1} className="m-0 flex flex-col gap-2 border-0 p-0 outline-none">
        <legend className="mb-2 text-16 font-semibold">{F.arrears}</legend>
        {ARREARS.map((k) => (
          <ChoiceCard key={k} type="radio" name="arrears" checked={f.arrearsDuration === k} onChange={() => update({ arrearsDuration: k })} label={F.arrearsOptions[k]} />
        ))}
        {errors.arrearsDuration ? (
          <span className="flex items-center gap-1 text-14 text-err">
            <Icon name="error" size={16} />
            {errors.arrearsDuration}
          </span>
        ) : null}
      </fieldset>
      <TextField
        id="field-propertyCity"
        label={F.city}
        size="lg"
        maxLength={100}
        value={f.propertyCity}
        onChange={(e) => update({ propertyCity: e.target.value })}
        error={errors.propertyCity}
      />
      <p className="m-0 flex items-start gap-1.5 text-14 text-muted">
        <Icon name="info" size={18} />
        {F.note}
      </p>
    </div>
  );
}

/* ───────── OA03 — situation and preference ───────── */

function SituationStep({ f, update, errors }: { f: Fields; update: (p: Partial<Fields>) => void; errors: Record<string, string> }) {
  const c = useRequestCopy();
  const S = c.wizard.situation;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 leading-9 font-bold">{S.heading}</h1>
      <p className="m-0 text-17 leading-7 text-charcoal">{S.lead}</p>
      <fieldset id="field-pathPreference" tabIndex={-1} className="m-0 flex flex-col gap-2 border-0 p-0 outline-none">
        <legend className="mb-2 text-16 font-semibold">{S.preference}</legend>
        {PREFERENCES.map((k) => (
          <ChoiceCard key={k} type="radio" name="preference" checked={f.pathPreference === k} onChange={() => update({ pathPreference: k })} label={S.options[k]} />
        ))}
        {errors.pathPreference ? (
          <span className="flex items-center gap-1 text-14 text-err">
            <Icon name="error" size={16} />
            {errors.pathPreference}
          </span>
        ) : null}
      </fieldset>
      <p className="m-0 flex items-start gap-1.5 rounded-[12px] border border-info-line bg-info-bg px-3.5 py-3 text-15 leading-6">
        <Icon name="info" size={20} className="text-info" />
        {S.note}
      </p>
      <AmountField label={<>{S.affordable} <span className="font-normal text-muted">{c.optional}</span></>} size="lg" value={f.affordableMonthly} onValueChange={(v) => update({ affordableMonthly: v })} />
      <Textarea
        label={S.situationText}
        optionalMark
        help={S.situationHelp}
        rows={4}
        maxLength={1500}
        value={f.situationText}
        onChange={(e) => update({ situationText: e.target.value })}
      />
    </div>
  );
}

/* ───────── OA04 — documents and consent ───────── */

function DocsConsentStep({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const D = c.wizard.docs;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 leading-9 font-bold">{D.heading}</h1>
      <p className="m-0 text-16 leading-7 text-charcoal">{D.lead}</p>
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {DOC_KINDS.map((k) => (
          <li key={k}>
            <DocRow reference={detail.reference} kind={k} label={D.kinds[k]} doc={detail.documents.find((d) => d.kind === k && !d.addedAfterSubmit) ?? null} />
          </li>
        ))}
      </ul>
      <ConsentBox detail={detail} />
    </div>
  );
}

export function DocRow({ reference, kind, label, doc, allowName }: { reference: string; kind: DocKindKey; label: string; doc: MyRequestDetail["documents"][number] | null; allowName?: boolean }) {
  const c = useRequestCopy();
  const D = c.wizard.docs;
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  // After submission every file is an addition (no replacement), so remember what was just added.
  const [added, setAdded] = useState<string | null>(null);
  const shownName = doc?.fileName ?? added;
  const onFile = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    setError(null);
    const form = new FormData();
    form.append("file", file);
    form.append("kind", kind);
    if (allowName && name.trim()) form.append("name", name.trim());
    try {
      await apiUpload(`/my/requests/${encodeURIComponent(reference)}/documents`, form);
      setAdded(file.name);
      router.refresh();
    } catch (e) {
      setError(isApiError(e) && e.code === "file_infected" ? D.scanFailed : requestErrorText(e, c));
    } finally {
      setBusy(false);
      if (input.current) input.current.value = "";
    }
  };
  return (
    <div className="flex flex-col gap-2 rounded-[12px] border border-line bg-white px-3.5 py-3">
      <div className="flex items-center gap-3">
        <Icon name={shownName ? "task" : "description"} size={24} className={shownName ? "text-ok" : "text-muted"} />
        <div className="flex min-w-0 flex-1 flex-col">
          <strong className="text-15">{label}</strong>
          {shownName ? (
            <span className="truncate text-13 text-muted">
              {D.uploaded} · <bdi dir="ltr">{shownName}</bdi>
            </span>
          ) : null}
        </div>
        <input ref={input} type="file" accept="application/pdf,image/jpeg,image/png" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => void onFile(e.target.files?.[0])} />
        <Button variant="secondary" size="lg" loading={busy} loadingLabel={D.uploading} onClick={() => input.current?.click()} className="min-h-11 text-15" aria-label={`${doc ? D.replace : D.upload} — ${label}`}>
          {doc ? D.replace : D.upload}
        </Button>
      </div>
      {allowName ? <TextField label={c.wizard.docs.kinds.other} size="md" value={name} onChange={(e) => setName(e.target.value)} maxLength={120} /> : null}
      {error ? (
        <span role="alert" className="flex items-center gap-1 text-14 text-err">
          <Icon name="error" size={16} />
          {error}
        </span>
      ) : null}
    </div>
  );
}

/** Documented consent to share with the named lender (V4 provisional text), confirmed with an SMS code. */
export function ConsentBox({ detail, onRecorded }: { detail: MyRequestDetail; onRecorded?: () => void }) {
  const c = useRequestCopy();
  const D = c.wizard.docs;
  const { numerals, t } = useI18n();
  const router = useRouter();
  const [checked, setChecked] = useState(false);
  const [phase, setPhase] = useState<{ kind: "idle" } | { kind: "code"; destination: string; sandbox: string | null }>({ kind: "idle" });
  const [code, setCode] = useState("");
  const [checkError, setCheckError] = useState<string | null>(null);
  const send = useRequestSubmit();
  const confirm = useRequestSubmit();

  if (detail.consent) {
    return (
      <Panel aria-labelledby="consent-h">
        <h2 id="consent-h" className="m-0 flex items-center gap-2 text-18 font-bold">
          <Icon name="verified_user" size={22} className="text-ok" />
          {D.recorded}
        </h2>
        <p className="m-0 text-15 leading-6">
          {D.recordedOn} <bdi dir="ltr">{formatDate(detail.consent.recordedAt, { numerals })}</bdi>
        </p>
        <details className="text-14 leading-6 text-charcoal">
          <summary className="min-h-11 cursor-pointer font-semibold">{D.viewText}</summary>
          <p className="m-0">{detail.consent.textSnapshot}</p>
        </details>
      </Panel>
    );
  }

  const requestCode = async () => {
    if (!checked) {
      setCheckError(D.consentCheckError);
      return;
    }
    const res = await send.run((key) =>
      apiSend<{ destination: string; sandboxCode?: string | null }>("POST", `/my/requests/${encodeURIComponent(detail.reference)}/consent/otp`, undefined, { idempotencyKey: key }),
    );
    if (res.ok) {
      setCode("");
      setPhase({ kind: "code", destination: res.data.destination, sandbox: res.data.sandboxCode ?? null });
    }
  };

  const confirmCode = async () => {
    const res = await confirm.run((key) =>
      apiSend("POST", `/my/requests/${encodeURIComponent(detail.reference)}/consent`, { code, accept: true }, { idempotencyKey: key }),
    );
    if (res.ok) {
      setPhase({ kind: "idle" });
      onRecorded?.();
      router.refresh();
    } else setCode("");
  };

  return (
    <Panel aria-labelledby="consent-h" className="border-line-strong">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="consent-h" className="m-0 text-18 font-bold">
          {D.consentTitle}
        </h2>
        <Tag tone="warn" icon="rule">
          {D.consentDraft}
        </Tag>
      </div>
      <p className="m-0 rounded-md bg-subtle p-3 text-15 leading-7">{detail.consentText.text}</p>
      <p className="m-0 text-14 text-muted">{D.consentWhy}</p>
      <Checkbox
        boxSize={24}
        checked={checked}
        onChange={(e) => {
          setChecked(e.target.checked);
          if (e.target.checked) setCheckError(null);
        }}
        error={checkError ?? undefined}
        label={D.consentCheck}
        disabled={phase.kind === "code"}
      />
      {send.error ? <Alert tone="err">{send.error}</Alert> : null}
      {phase.kind === "idle" ? (
        <Button variant="secondary" size="xl" fullWidth icon="sms" loading={send.busy} onClick={() => void requestCode()}>
          {D.sendCode}
        </Button>
      ) : (
        <div className="flex flex-col gap-3">
          <p className="m-0 text-15">
            {D.codeSentTo}{" "}
            <bdi dir="ltr" className="font-mono">
              {phase.destination}
            </bdi>
          </p>
          {phase.sandbox ? <SandboxCodeBox title={t.sandbox.title} code={phase.sandbox} note={t.sandbox.note} /> : null}
          <OtpInput label={D.codeLabel} value={code} onChange={setCode} error={Boolean(confirm.error)} boxHeight={52} autoFocus />
          {confirm.error ? <Alert tone="err">{confirm.error}</Alert> : null}
          <Button size="xl" fullWidth loading={confirm.busy} disabled={code.length !== 6} onClick={() => void confirmCode()}>
            {D.confirm}
          </Button>
        </div>
      )}
    </Panel>
  );
}

/* ───────── OA05 — review ───────── */

function ReviewStep({ detail, institutions }: { detail: MyRequestDetail; institutions: Institution[] }) {
  const c = useRequestCopy();
  const R = c.wizard.review;
  const W = c.wizard;
  const { locale, numerals } = useI18n();
  const router = useRouter();
  const [ack, setAck] = useState(false);
  const [ackError, setAckError] = useState<string | null>(null);
  const submit = useRequestSubmit();
  const f = detail.fields;
  const inst = detail.institution ? institutions.find((i) => i.id === detail.institution!.id) : null;
  const lenderName = inst && locale === "en" && inst.nameEn ? inst.nameEn : detail.institutionName;
  const money = (v: number | null) => (v === null ? "—" : formatMoney(v, { locale, numerals }));
  const blockers: string[] = [...detail.missing];
  if (!detail.consent) blockers.push(R.consent);

  const send = async () => {
    if (detail.duplicate && !ack) {
      setAckError(R.duplicateAckError);
      return;
    }
    const res = await submit.run((key) =>
      apiSend("POST", `/my/requests/${encodeURIComponent(detail.reference)}/submit`, { acknowledgeDuplicate: ack }, { idempotencyKey: key }),
    );
    if (res.ok) router.push(`/my/requests/${encodeURIComponent(detail.reference)}/submitted`);
  };

  const row = (key: string, value: ReactNode, step: number) => ({
    key,
    value: (
      <span className="flex items-start justify-between gap-2">
        <span className="min-w-0 break-words">{value}</span>
        <Link href={`/my/requests/${encodeURIComponent(detail.reference)}/apply?step=${step}`} className="shrink-0 text-14 font-semibold" aria-label={`${c.edit} — ${key}`}>
          {c.edit}
        </Link>
      </span>
    ),
  });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 leading-9 font-bold">{R.heading}</h1>
      <Panel>
        <KeyValueList
          rows={[
            row(R.lender, lenderName || "—", 1),
            row(R.name, f.applicantFullName || "—", 2),
            row(R.contract, f.contractNumber ? <bdi dir="ltr" className="font-mono">{f.contractNumber}</bdi> : "—", 2),
            row(R.installment, money(f.monthlyInstallment), 2),
            row(R.arrears, f.arrearsDuration ? W.finance.arrearsOptions[f.arrearsDuration] : "—", 2),
            row(R.city, f.propertyCity || "—", 2),
            row(R.preference, f.pathPreference ? W.situation.options[f.pathPreference] : "—", 3),
            ...(f.affordableMonthly !== null ? [row(R.affordable, money(f.affordableMonthly), 3)] : []),
            ...(f.situationText ? [row(R.situation, f.situationText, 3)] : []),
            row(R.documents, detail.documents.length ? detail.documents.map((d) => d.name).join("، ") : R.noDocuments, 4),
            row(
              R.consent,
              detail.consent ? (
                <>
                  {W.docs.recorded} · <bdi dir="ltr">{formatDate(detail.consent.recordedAt, { numerals })}</bdi>
                </>
              ) : (
                R.consentMissing
              ),
              4,
            ),
          ]}
        />
      </Panel>

      {detail.duplicate ? (
        <Alert tone="warn" title={R.duplicateTitle}>
          <p className="m-0">{R.duplicateBody(detail.duplicate.reference)}</p>
          <Link href={`/my/requests/${encodeURIComponent(detail.duplicate.reference)}`} className="mt-1 inline-flex min-h-11 items-center font-semibold">
            {R.duplicateLink}
          </Link>
          <Checkbox
            boxSize={24}
            checked={ack}
            onChange={(e) => {
              setAck(e.target.checked);
              if (e.target.checked) setAckError(null);
            }}
            error={ackError ?? undefined}
            label={R.duplicateAck}
          />
        </Alert>
      ) : null}

      <p className="m-0 flex items-start gap-1.5 text-15 leading-6 text-charcoal">
        <Icon name="lock" size={20} />
        {R.lockNote}
      </p>

      {blockers.length > 0 ? (
        <Alert tone="info" title={R.cantSubmit} id="submit-blockers">
          <ul className="m-0 ps-5">
            {blockers.map((b) => (
              <li key={b}>{b}</li>
            ))}
          </ul>
        </Alert>
      ) : null}
      {submit.error ? <Alert tone="err">{submit.error}</Alert> : null}
      <Button
        size="xl"
        fullWidth
        loading={submit.busy}
        loadingLabel={R.submitting}
        softDisabled={blockers.length > 0}
        aria-describedby={blockers.length > 0 ? "submit-blockers" : undefined}
        onClick={() => void send()}
      >
        {R.submit}
      </Button>
    </div>
  );
}
