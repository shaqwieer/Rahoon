"use client";

import Link from "next/link";
import { useMemo, useRef, useState, type FormEvent } from "react";
import { Alert, SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { OtpInput } from "@/components/ui/OtpInput";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { autoOtpCode, type OtpIssued } from "@/lib/api/otp";
import { cn } from "@/lib/cn";
import { formatCountdown } from "@/lib/format";
import { useNow } from "@/lib/hooks";
import { normalizeSaudiMobile } from "@/lib/market/numbers";

export interface SignedIn {
  next: string;
  firstTime: boolean;
  needsName: boolean;
  smsVerified: boolean;
  /** The name typed on this form (when asked). */
  name?: string;
}

type Step = { kind: "phone" } | { kind: "code"; destination: string; resendAt: number; sandboxCode: string | null; auto: boolean } | { kind: "locked"; message: string };

/**
 * Mobile-first sign-in (one account per mobile; the same account sells and buys). When the API runs without an SMS
 * provider (`otpRequired: false`) the screen says plainly that no code was sent and the number is not verified —
 * it never presents that as a verification.
 */
export function PhoneSignIn({
  askName = false,
  initialName = "",
  initialPhone = "",
  submitLabel = "دخول",
  onSignedIn,
  compact,
}: {
  askName?: boolean;
  initialName?: string;
  initialPhone?: string;
  submitLabel?: string;
  onSignedIn: (r: SignedIn) => void | Promise<void>;
  compact?: boolean;
}) {
  const [step, setStep] = useState<Step>({ kind: "phone" });
  const [phone, setPhone] = useState(initialPhone);
  const [name, setName] = useState(initialName);
  const [code, setCode] = useState("");
  const [terms, setTerms] = useState(false);
  const [errors, setErrors] = useState<{ phone?: string; name?: string; terms?: string; form?: string }>({});
  const [loading, setLoading] = useState(false);
  const phoneRef = useRef<HTMLInputElement>(null);
  const otpRef = useRef<HTMLInputElement>(null);
  const key = useIdempotencyKey();
  const now = useNow();
  const secondsLeft = useMemo(() => (step.kind === "code" && now ? Math.ceil((step.resendAt - now) / 1000) : null), [step, now]);

  const start = async (e?: FormEvent) => {
    e?.preventDefault();
    const mobile = normalizeSaudiMobile(phone);
    const errs: typeof errors = {};
    if (!mobile) errs.phone = "أدخل رقم جوال سعودي صحيحًا يبدأ بـ 05.";
    if (askName && name.trim().length < 2) errs.name = "اكتب اسمك.";
    if (errs.phone || errs.name) {
      setErrors(errs);
      phoneRef.current?.focus();
      return;
    }
    setErrors({});
    setLoading(true);
    try {
      const res = await apiSend<OtpIssued>("POST", "/auth/phone/start", { phone: mobile }, { idempotencyKey: key.get() });
      key.reset();
      const auto = autoOtpCode(res);
      setCode(auto ?? "");
      setStep({ kind: "code", destination: res.destination, resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000, sandboxCode: res.sandboxCode ?? null, auto: Boolean(auto) });
    } catch (err) {
      key.reset();
      if (!isApiError(err)) setErrors({ form: "تعذّر الاتصال. تحقق من الإنترنت وحاول مرة أخرى." });
      else if (err.status === 423) setStep({ kind: "locked", message: err.title });
      else setErrors({ phone: err.fieldError("phone") ?? undefined, form: err.fieldError("phone") ? undefined : err.title });
    } finally {
      setLoading(false);
    }
  };

  const resend = async () => {
    setLoading(true);
    try {
      const res = await apiSend<OtpIssued>("POST", "/auth/phone/resend");
      const auto = autoOtpCode(res);
      if (auto) setCode(auto);
      setStep({ kind: "code", destination: res.destination, resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000, sandboxCode: res.sandboxCode ?? null, auto: Boolean(auto) });
      setErrors({});
    } catch (err) {
      setErrors({ form: isApiError(err) ? err.title : "تعذّر الاتصال." });
    } finally {
      setLoading(false);
    }
  };

  const verify = async (e: FormEvent) => {
    e.preventDefault();
    if (!terms) {
      setErrors({ terms: "للمتابعة، وافق على الشروط وسياسة الخصوصية." });
      return;
    }
    if (code.length !== 6) {
      setErrors({ form: "أدخل الرمز المكوّن من 6 أرقام." });
      otpRef.current?.focus();
      return;
    }
    setErrors({});
    setLoading(true);
    try {
      const res = await apiSend<SignedIn>("POST", "/auth/phone/verify", { code, acceptTerms: terms, name: askName ? name.trim() : undefined });
      await onSignedIn({ ...res, name: askName ? name.trim() : undefined });
    } catch (err) {
      setLoading(false);
      setCode(step.kind === "code" && step.auto ? code : "");
      if (!isApiError(err)) setErrors({ form: "تعذّر الاتصال. تحقق من الإنترنت وحاول مرة أخرى." });
      else if (err.code === "otp_invalid") setErrors({ form: err.title });
      else if (err.status === 423) setStep({ kind: "locked", message: err.title });
      else if (err.code === "otp_expired") setErrors({ form: "انتهت صلاحية الرمز. اطلب رمزًا جديدًا." });
      else if (err.status === 401) {
        setStep({ kind: "phone" });
        setErrors({ form: err.title });
      } else setErrors({ form: err.title, name: err.fieldError("name") ?? undefined });
    }
  };

  if (step.kind === "locked")
    return (
      <div role="alert" className="flex flex-col gap-3">
        <Alert tone="warn" title="أُوقف الدخول مؤقتًا">
          {step.message}
        </Alert>
        <Button variant="secondary" onClick={() => setStep({ kind: "phone" })}>
          حاول مرة أخرى
        </Button>
      </div>
    );

  if (step.kind === "code") {
    const ready = secondsLeft !== null && secondsLeft <= 0;
    return (
      <form noValidate onSubmit={verify} className="flex flex-col gap-4">
        {step.auto ? (
          <Alert tone="warn" title="لم يُرسل رمز تحقق">
            خدمة الرسائل النصية غير مفعّلة في هذه البيئة التجريبية، لذلك لن يصلك رمز ولن نتحقق من ملكيتك للرقم{" "}
            <bdi dir="ltr" className="font-mono">
              {step.destination}
            </bdi>
            . تابع للدخول بهذا الرقم.
          </Alert>
        ) : (
          <>
            <p className="m-0 text-15 leading-7">
              أرسلنا رمزًا من 6 أرقام إلى{" "}
              <bdi dir="ltr" className="font-mono">
                {step.destination}
              </bdi>
              .
            </p>
            {step.sandboxCode ? <SandboxCodeBox title="بيئة تجريبية — الرمز:" code={step.sandboxCode} note="لا تُرسل رسائل نصية فعلية في هذه البيئة." /> : null}
            <OtpInput ref={otpRef} label="رمز التحقق" value={code} onChange={setCode} error={Boolean(errors.form)} disabled={loading} autoFocus boxHeight={compact ? 52 : 56} />
            <span aria-live="polite" className="flex items-center gap-1 text-13 text-muted">
              <Icon name="schedule" size={16} />
              صالح 5 دقائق{!ready && secondsLeft !== null ? ` · إعادة الإرسال بعد ${formatCountdown(secondsLeft)}` : null}
            </span>
            {ready ? (
              <Button variant="text" onClick={() => void resend()} loading={loading} className="self-start">
                أرسل رمزًا جديدًا
              </Button>
            ) : null}
          </>
        )}
        {errors.form ? <Alert tone="err">{errors.form}</Alert> : null}
        <Checkbox
          checked={terms}
          onChange={(e) => {
            setTerms(e.target.checked);
            if (e.target.checked) setErrors((x) => ({ ...x, terms: undefined }));
          }}
          error={errors.terms}
          label={
            <>
              أوافق على{" "}
              <Link href="/terms" target="_blank">
                شروط الاستخدام
              </Link>{" "}
              و
              <Link href="/privacy" target="_blank">
                سياسة الخصوصية
              </Link>{" "}
              (مسودة)
            </>
          }
        />
        <Button type="submit" size="lg" loading={loading} fullWidth={compact}>
          {step.auto ? "متابعة بدون رمز" : submitLabel}
        </Button>
        <Button variant="text" onClick={() => setStep({ kind: "phone" })} className="self-start">
          تغيير الرقم
        </Button>
      </form>
    );
  }

  return (
    <form noValidate onSubmit={start} className={cn("flex flex-col gap-4")}>
      {errors.form ? <Alert tone="err">{errors.form}</Alert> : null}
      {askName ? (
        <TextField
          label="الاسم"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            setErrors((x) => ({ ...x, name: undefined }));
          }}
          error={errors.name}
          autoComplete="name"
          size="lg"
          requiredMark
        />
      ) : null}
      <TextField
        ref={phoneRef}
        label="رقم الجوال"
        help="رقم سعودي يبدأ بـ 05. نستخدمه لحسابك وللتواصل بشأن طلباتك."
        value={phone}
        onChange={(e) => {
          setPhone(e.target.value);
          setErrors((x) => ({ ...x, phone: undefined }));
        }}
        error={errors.phone}
        inputMode="tel"
        autoComplete="tel"
        size="lg"
        ltr
        mono
        requiredMark
      />
      <Button type="submit" size="lg" loading={loading} fullWidth={compact}>
        متابعة
      </Button>
    </form>
  );
}
