"use client";

import Link from "next/link";
import { useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import { DebtorTop } from "@/components/shell/OwnerShell";
import { Alert, SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { OtpInput } from "@/components/ui/OtpInput";
import { SkipLink } from "@/components/ui/SkipLink";
import { IntegrationStateTag, type IntegrationState } from "@/components/ui/Status";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { formatCountdown } from "@/lib/format";
import { useNow } from "@/lib/hooks";
import { useI18n } from "@/lib/i18n/client";
import { hardNavigate } from "@/lib/navigation";

interface OtpIssued {
  destination: string;
  resendInSeconds: number;
  sandboxCode?: string | null;
}

type Step =
  | { kind: "id" }
  | { kind: "otp"; destination: string; resendAt: number; sandboxCode: string | null }
  | { kind: "locked"; message: string };

const toLatinDigits = (v: string) => v.replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d)));

/**
 * OR01 → OR02 (B13): one flow for registering and signing in. The API answers the same way whether the ID is
 * registered or not (anti-enumeration), so terms are accepted on the code screen every time. Interim rules:
 * mobile code only (national digital identity «unavailable», Q10), no password (Q15), no «مجاني» (Q9).
 */
export function StartFlow({ mode, next, nationalIdState }: { mode: "register" | "signin"; next: string; nationalIdState: IntegrationState }) {
  const { t } = useI18n();
  const S = t.individual.start;
  const [step, setStep] = useState<Step>({ kind: "id" });
  const [nationalId, setNationalId] = useState("");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [terms, setTerms] = useState(false);
  const [awareness, setAwareness] = useState(false);
  const [errors, setErrors] = useState<{ nationalId?: string; phone?: string; terms?: string; form?: string }>({});
  const [loading, setLoading] = useState(false);
  const idRef = useRef<HTMLInputElement>(null);
  const phoneRef = useRef<HTMLInputElement>(null);
  const otpRef = useRef<HTMLInputElement>(null);
  const sendKey = useIdempotencyKey();
  const now = useNow();
  const secondsLeft = useMemo(() => (step.kind === "otp" && now ? Math.ceil((step.resendAt - now) / 1000) : null), [step, now]);
  const title = mode === "signin" ? S.signInTitle : S.registerTitle;

  const sendCode = async (e?: FormEvent) => {
    e?.preventDefault();
    const id = toLatinDigits(nationalId).replace(/\D/g, "");
    const mobile = toLatinDigits(phone).replace(/\D/g, "");
    const next: typeof errors = {};
    if (!/^[12]\d{9}$/.test(id)) next.nationalId = S.idError;
    if (!/^05\d{8}$/.test(mobile)) next.phone = S.phoneError;
    if (next.nationalId || next.phone) {
      setErrors(next);
      (next.nationalId ? idRef : phoneRef).current?.focus();
      return;
    }
    setErrors({});
    setLoading(true);
    try {
      const res = await apiSend<OtpIssued>("POST", "/auth/individual/start", { nationalId: id, phone: mobile }, { idempotencyKey: sendKey.get() });
      sendKey.reset();
      setCode("");
      setStep({ kind: "otp", destination: res.destination, resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000, sandboxCode: res.sandboxCode ?? null });
    } catch (err) {
      sendKey.reset();
      if (!isApiError(err)) setErrors({ form: t.auth.login.network });
      else if (err.status === 423) setStep({ kind: "locked", message: err.title });
      else if (err.fieldError("nationalId") || err.fieldError("phone"))
        setErrors({ nationalId: err.fieldError("nationalId") ?? undefined, phone: err.fieldError("phone") ?? undefined });
      else setErrors({ form: err.title || t.auth.login.network });
    } finally {
      setLoading(false);
    }
  };

  const resend = async () => {
    setLoading(true);
    try {
      const res = await apiSend<OtpIssued>("POST", "/auth/individual/resend");
      setStep({ kind: "otp", destination: res.destination, resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000, sandboxCode: res.sandboxCode ?? null });
    } catch (err) {
      setErrors({ form: isApiError(err) ? err.title : t.auth.login.network });
    } finally {
      setLoading(false);
    }
  };

  const verify = async (e: FormEvent) => {
    e.preventDefault();
    if (!terms) {
      setErrors({ terms: S.termsRequired });
      return;
    }
    if (code.length !== 6) {
      setErrors({ form: t.auth.mfa.codeRequired });
      otpRef.current?.focus();
      return;
    }
    setErrors({});
    setLoading(true);
    try {
      const res = await apiSend<{ next: string }>("POST", "/auth/individual/verify", { code, acceptTerms: terms, awarenessOptIn: awareness });
      hardNavigate(next || res.next, "/my");
    } catch (err) {
      setLoading(false);
      setCode("");
      if (!isApiError(err)) setErrors({ form: t.auth.login.network });
      else if (err.code === "otp_invalid") {
        const left = Number(err.reasons?.[0]);
        setErrors({ form: Number.isFinite(left) ? t.auth.mfa.wrongCode(left) : err.title });
      } else if (err.status === 423) setStep({ kind: "locked", message: err.title });
      else if (err.code === "otp_expired") setErrors({ form: t.auth.mfa.expired });
      else if (err.fieldError("acceptTerms")) setErrors({ terms: err.fieldError("acceptTerms") ?? S.termsRequired });
      else if (err.status === 401) {
        setStep({ kind: "id" });
        setErrors({ form: err.title || t.auth.mfa.sessionExpired });
      } else setErrors({ form: err.title || t.auth.login.network });
      otpRef.current?.focus();
    }
  };

  if (step.kind === "locked") {
    return (
      <Frame top={<DebtorTop title={title} back={{ onClick: () => setStep({ kind: "id" }) }} showBell={false} />}>
        <div role="alert" className="flex flex-col gap-4 pt-4">
          <span className="flex size-14 items-center justify-center rounded-full bg-warn-bg text-warn">
            <Icon name="timer" size={30} />
          </span>
          <h1 className="m-0 text-26 leading-[38px] font-bold">{S.lockedTitle}</h1>
          <p className="m-0 text-17 leading-[29px]">{step.message}</p>
          <Button variant="secondary" size="xl" fullWidth onClick={() => setStep({ kind: "id" })}>
            {S.again}
          </Button>
        </div>
      </Frame>
    );
  }

  if (step.kind === "otp") {
    const ready = secondsLeft !== null && secondsLeft <= 0;
    return (
      <Frame top={<DebtorTop title={title} sub={S.codeSub} back={{ onClick: () => setStep({ kind: "id" }) }} showBell={false} />}>
        <form noValidate onSubmit={verify} aria-labelledby="code-title" className="flex flex-1 flex-col gap-[18px]">
          <h1 id="code-title" className="m-0 text-24 leading-9 font-bold">
            {S.codeHeading}
          </h1>
          <p className="m-0 text-17 leading-[28px]">
            {S.codeLead}{" "}
            <bdi dir="ltr" className="font-mono">
              {step.destination}
            </bdi>
            .
          </p>
          {step.sandboxCode ? <SandboxCodeBox title={t.sandbox.title} code={step.sandboxCode} note={t.sandbox.note} /> : null}
          <OtpInput ref={otpRef} label={S.codeLabel} value={code} onChange={setCode} error={Boolean(errors.form)} disabled={loading} autoFocus boxHeight={56} describedBy="code-hint" />
          <span id="code-hint" aria-live="polite" className={cn("flex items-center gap-1 text-14", errors.form ? "text-err" : "text-muted")}>
            {errors.form ? (
              <>
                <Icon name="error" size={16} />
                {errors.form}
              </>
            ) : (
              <>
                <Icon name="schedule" size={16} />
                {S.validFor}
                {!ready && secondsLeft !== null ? ` · ${S.resendIn(formatCountdown(secondsLeft))}` : null}
              </>
            )}
          </span>
          {ready ? (
            <Button variant="text" size="lg" onClick={() => void resend()} loading={loading} className="self-start text-16">
              {S.resend}
            </Button>
          ) : null}
          <p className="m-0 text-14 leading-[22px] text-muted">{S.notReceived}</p>
          <div className="flex flex-col gap-3 rounded-md border border-line bg-white p-4">
            <Checkbox
              boxSize={24}
              checked={terms}
              onChange={(e) => {
                setTerms(e.target.checked);
                if (e.target.checked) setErrors((x) => ({ ...x, terms: undefined }));
              }}
              error={errors.terms}
              label={
                <>
                  {S.termsLead}{" "}
                  <Link href="/terms" target="_blank">
                    {S.termsLink}
                  </Link>{" "}
                  {S.and}
                  <Link href="/privacy" target="_blank">
                    {S.privacyLink}
                  </Link>
                </>
              }
            />
            <Checkbox boxSize={24} checked={awareness} onChange={(e) => setAwareness(e.target.checked)} label={S.awareness} />
          </div>
          <Button type="submit" size="xl" fullWidth loading={loading} loadingLabel={S.verifying} className="mt-auto">
            {mode === "signin" ? S.signIn : S.createAccount}
          </Button>
          <Button variant="text" size="lg" onClick={() => setStep({ kind: "id" })} className="self-center text-15">
            {S.changeNumber}
          </Button>
        </form>
      </Frame>
    );
  }

  return (
    <Frame top={<DebtorTop title={title} sub={mode === "signin" ? S.signInSub : S.registerSub} back={{ href: "/" }} showBell={false} />}>
      <form noValidate onSubmit={sendCode} aria-labelledby="start-title" className="flex flex-1 flex-col gap-[18px]">
        <h1 id="start-title" className="m-0 text-24 leading-9 font-bold">
          {S.heading}
        </h1>
        <p className="m-0 text-17 leading-[28px] text-charcoal">{S.intro}</p>
        {errors.form ? <Alert tone="err">{errors.form}</Alert> : null}
        <TextField
          ref={idRef}
          label={S.idLabel}
          help={S.idHint}
          size="xl"
          ltr
          mono
          inputMode="numeric"
          autoComplete="off"
          maxLength={10}
          value={nationalId}
          onChange={(e) => {
            setNationalId(toLatinDigits(e.target.value).replace(/\D/g, "").slice(0, 10));
            setErrors((x) => ({ ...x, nationalId: undefined }));
          }}
          error={errors.nationalId}
        />
        <TextField
          ref={phoneRef}
          label={S.phoneLabel}
          help={S.phoneHint}
          size="xl"
          ltr
          mono
          type="tel"
          inputMode="tel"
          autoComplete="tel-national"
          placeholder="05XXXXXXXX"
          maxLength={10}
          value={phone}
          onChange={(e) => {
            setPhone(toLatinDigits(e.target.value).replace(/\D/g, "").slice(0, 10));
            setErrors((x) => ({ ...x, phone: undefined }));
          }}
          error={errors.phone}
        />
        <div className="flex flex-col items-center gap-1.5 rounded-md border border-dashed border-line-strong bg-white p-3 text-center text-14">
          <span className="flex items-center gap-2 font-semibold">
            <Icon name="badge" size={20} />
            {S.digitalId}
          </span>
          <IntegrationStateTag state={nationalIdState} />
        </div>
        <Button type="submit" size="xl" fullWidth loading={loading} loadingLabel={S.sending} className="mt-auto">
          {S.sendCode}
        </Button>
        <Link href={mode === "signin" ? "/start" : "/start?mode=signin"} className="flex min-h-11 items-center justify-center text-16 font-semibold">
          {mode === "signin" ? S.toRegister : S.toSignIn}
        </Link>
      </form>
    </Frame>
  );
}

function Frame({ top, children }: { top: ReactNode; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-warm">
      <SkipLink />
      {top}
      <main id="main" tabIndex={-1} className="mx-auto flex w-full max-w-[480px] flex-1 flex-col gap-[18px] px-[22px] py-6 outline-none">
        {children}
      </main>
    </div>
  );
}
