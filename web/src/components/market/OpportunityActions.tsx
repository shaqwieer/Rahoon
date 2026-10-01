"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Chips } from "@/components/market/DynamicField";
import { Amount, StatusBadge } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { toLatinDigits } from "@/lib/market/numbers";
import type { Fit } from "@/lib/market/types";

/** «حاسبة الفرصة»: the visitor's own figures against this opportunity's published terms (server rules, nothing stored). */
export function OpportunityCalculator({ reference, frequencies }: { reference: string; frequencies: { value: string; label: string }[] }) {
  const [available, setAvailable] = useState("");
  const [inst, setInst] = useState("");
  const [freq, setFreq] = useState("monthly");
  const [res, setRes] = useState<{ fit: Fit; leftAfterNow: number | null } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const num = (v: string) => (v ? Number(toLatinDigits(v).replace(/[^\d.]/g, "")) : null);
  const run = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      setRes(await apiSend("POST", `/market/calc/opportunities/${reference}/fit`, { availableNow: num(available), installmentComfort: num(inst), installmentFrequency: freq }));
    } catch (err) {
      setError(isApiError(err) ? err.title : "تعذّر الحساب الآن.");
    } finally {
      setBusy(false);
    }
  };
  return (
    <form onSubmit={run} className="flex flex-col gap-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <TextField label="المبلغ المتاح لك الآن" inputMode="numeric" ltr value={available} onChange={(e) => setAvailable(e.target.value)} endAdornment="ر.س" />
        <div className="flex gap-2">
          <TextField label="القسط المريح" inputMode="numeric" ltr value={inst} onChange={(e) => setInst(e.target.value)} containerClassName="flex-1" />
          <label className="flex flex-col gap-1.5 text-14 font-semibold">
            الدورية
            <select value={freq} onChange={(e) => setFreq(e.target.value)} className="min-h-12 rounded-sm border border-line-strong bg-white px-2">
              {frequencies.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
            </select>
          </label>
        </div>
      </div>
      <Button type="submit" variant="secondary" loading={busy} className="self-start">احسب</Button>
      {error ? <Alert tone="err" compact>{error}</Alert> : null}
      {res ? (
        <div className={cn("flex flex-col gap-1.5 rounded-md border p-3 text-14", res.fit.fits ? "border-ok-line bg-ok-bg" : "border-warn-line bg-warn-bg")} role="status">
          <strong>{!res.fit.comparable ? "لا يمكن المقارنة بعد" : res.fit.fits ? "تبدو مناسبة لأرقامك" : "لا تناسب كل أرقامك"}</strong>
          {res.leftAfterNow !== null ? (
            <span>
              يتبقى لديك بعد الدفع الآن: <Amount value={res.leftAfterNow} size="sm" strong />
            </span>
          ) : null}
          {[...res.fit.reasons, ...res.fit.limits].map((r) => <span key={r}>• {r}</span>)}
          <span className="text-12 text-muted">نتيجة تقديرية من أرقامك، ليست موافقة تمويل ولا عرضًا.</span>
        </div>
      ) : null}
    </form>
  );
}

/** Interest tied to this opportunity and its current terms version. It reaches the Rahoon team; it reserves nothing. */
export function InterestBox({ reference, signedIn, isOwn, existing, defaultName }: {
  reference: string;
  signedIn: boolean;
  isOwn?: boolean;
  existing: { reference: string; status: string; statusLabel: string } | null;
  defaultName: string | null;
}) {
  const [done, setDone] = useState(existing);
  const [message, setMessage] = useState("");
  const [pref, setPref] = useState("any");
  const [name, setName] = useState(defaultName ?? "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (!signedIn)
    return (
      <div className="flex flex-col gap-3">
        <p className="m-0 text-14 leading-6">ادخل برقم جوالك لإرسال اهتمامك؛ يصل لفريق رهون ويتابعه معك.</p>
        <Link href={`/signin?next=${encodeURIComponent(`/opportunities/${reference}#interest`)}`} className={buttonClasses({ variant: "primary", size: "lg" })}>
          الدخول وإرسال الاهتمام
        </Link>
      </div>
    );
  if (isOwn) return <p className="m-0 text-14 text-muted">هذه فرصتك أنت.</p>;
  if (done)
    return (
      <div className="flex flex-col gap-2" role="status">
        <span className="flex items-center gap-2 text-15 font-semibold">
          <Icon name="check_circle" size={22} className="text-ok" />
          أرسلت اهتمامك
          <StatusBadge status={done.status} label={done.statusLabel} />
        </span>
        <span className="text-13 text-muted">رقم الاهتمام <bdi dir="ltr" className="font-mono">{done.reference}</bdi>. الاهتمام لا يحجز العقار ولا يعد عرضًا ملزمًا.</span>
        <Link href="/account/interests" className="text-14">متابعة اهتماماتي</Link>
      </div>
    );

  const send = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const r = await apiSend<{ reference: string; status: string; statusLabel: string }>("POST", `/market/opportunities/${reference}/interest`, {
        message: message.trim() || null, contactPreference: pref, contactName: name.trim() || null,
      });
      setDone(r);
    } catch (err) {
      setError(isApiError(err) ? (err.fieldError("contactName") ?? err.title) : "تعذّر الإرسال. تحقق من الاتصال.");
    } finally {
      setBusy(false);
    }
  };
  return (
    <form onSubmit={send} noValidate className="flex flex-col gap-3">
      {error ? <Alert tone="err" compact>{error}</Alert> : null}
      {!defaultName ? <TextField label="اسمك" value={name} onChange={(e) => setName(e.target.value)} autoComplete="name" /> : null}
      <fieldset className="m-0 flex flex-col gap-1.5 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">طريقة التواصل المفضلة</legend>
        <Chips name="pref" options={[{ value: "call", label: "اتصال" }, { value: "whatsapp", label: "واتساب" }, { value: "any", label: "أيهما" }]} value={pref} onChange={setPref} />
      </fieldset>
      <Textarea label="رسالة للفريق (اختياري)" value={message} onChange={(e) => setMessage(e.target.value)} rows={3} maxLength={1000} />
      <Button type="submit" size="lg" loading={busy}>مهتم بالفرصة</Button>
      <span className="text-12 text-muted">يصل اهتمامك لفريق رهون مع أرقام هذا الإصدار من الفرصة. لا يحجز العقار ولا يلزمك بشيء.</span>
    </form>
  );
}
