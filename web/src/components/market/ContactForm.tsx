"use client";

import { useState, type FormEvent } from "react";
import { M } from "@/components/market/copy";
import { Alert, Button, Checkbox, ErrorSummary, Select, Textarea, TextField } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { normalizeSaudiMobile } from "@/lib/market/numbers";

type Field = "name" | "phone" | "topic" | "message" | "consent";

/** General enquiry stored for the Rahoon team (`POST /api/market/contact`). The text stays in the form on failure. */
export function ContactForm() {
  const C = M.contact;
  const key = useIdempotencyKey();
  const [form, setForm] = useState({ name: "", phone: "", topic: "", message: "", consent: false });
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const [sentRef, setSentRef] = useState<string | null>(null);

  const set = <K extends keyof typeof form>(k: K, v: (typeof form)[K]) => {
    setForm((f) => ({ ...f, [k]: v }));
    key.reset();
  };

  const validate = () => {
    const e: Partial<Record<Field, string>> = {};
    if (form.name.trim().length < 2) e.name = "اكتب اسمك.";
    if (!normalizeSaudiMobile(form.phone)) e.phone = "أدخل رقم جوال سعودي صحيحًا يبدأ بـ 05.";
    if (!form.topic) e.topic = "اختر الموضوع.";
    if (form.message.trim().length < 10) e.message = "اكتب رسالتك في 10 أحرف على الأقل.";
    if (!form.consent) e.consent = "للإرسال، وافق على تواصل الفريق معك.";
    return e;
  };

  const submit = async (ev: FormEvent) => {
    ev.preventDefault();
    const e = validate();
    setErrors(e);
    setAttempt((n) => n + 1);
    setFailed(false);
    if (Object.keys(e).length > 0) return;
    setBusy(true);
    try {
      const res = await apiSend<{ reference: string }>(
        "POST",
        "/market/contact",
        { name: form.name.trim(), phone: normalizeSaudiMobile(form.phone), topic: form.topic, message: form.message.trim(), consent: form.consent },
        { idempotencyKey: key.get() },
      );
      setSentRef(res.reference);
    } catch (err) {
      if (isApiError(err) && err.errors) {
        const fe: Partial<Record<Field, string>> = {};
        for (const f of ["name", "phone", "topic", "message", "consent"] as Field[]) {
          const msg = err.fieldError(f);
          if (msg) fe[f] = msg;
        }
        setErrors(fe);
        setAttempt((n) => n + 1);
      } else setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  if (sentRef) {
    return (
      <Alert tone="ok" title={C.sent} role="status">
        {C.sentBody.replace("{ref}", sentRef)}
      </Alert>
    );
  }

  const summary = (Object.entries(errors) as [Field, string][]).map(([f, message]) => ({ fieldId: `contact-${f}`, message }));
  return (
    <form noValidate onSubmit={submit} className="flex max-w-[640px] flex-col gap-5 rounded-lg border border-line bg-white p-5 shadow-1 md:p-7">
      <ErrorSummary errors={summary} focusKey={attempt} />
      {failed ? <Alert tone="err">{C.failed}</Alert> : null}
      <TextField id="contact-name" label={C.name} value={form.name} onChange={(e) => set("name", e.target.value)} error={errors.name} autoComplete="name" requiredMark />
      <TextField
        id="contact-phone"
        label={C.phone}
        help={C.phoneHelp}
        value={form.phone}
        onChange={(e) => set("phone", e.target.value)}
        error={errors.phone}
        inputMode="tel"
        autoComplete="tel"
        ltr
        requiredMark
      />
      <Select id="contact-topic" label={C.topic} options={[...C.topics]} placeholder="اختر" value={form.topic} onChange={(e) => set("topic", e.target.value)} error={errors.topic} requiredMark />
      <Textarea id="contact-message" label={C.message} value={form.message} onChange={(e) => set("message", e.target.value)} error={errors.message} maxLength={2000} rows={5} requiredMark />
      <Checkbox id="contact-consent" label={C.consent} checked={form.consent} onChange={(e) => set("consent", e.target.checked)} error={errors.consent} />
      <Button type="submit" size="lg" loading={busy} className="self-start">
        {busy ? C.sending : C.send}
      </Button>
    </form>
  );
}
