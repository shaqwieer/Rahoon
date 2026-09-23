"use client";

import { useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import { DebtorTop } from "@/components/shell/OwnerShell";
import { Alert, SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { OtpInput } from "@/components/ui/OtpInput";
import { IntegrationStateTag, type IntegrationState } from "@/components/ui/Status";
import { SkipLink } from "@/components/ui/SkipLink";
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
  | { kind: "locked"; message: string }
  | { kind: "invalid"; message: string };

/**
 * Owner sign-in: step 1 verifies the last 4 digits of the national ID and sends an SMS code to the phone
 * registered by the lender (the owner cannot change it here); step 2 verifies the code and opens /owner.
 */
export function OwnerVerify({ token, phoneMasked, nationalIdState }: { token: string; phoneMasked: string; nationalIdState: IntegrationState }) {
  const { t } = useI18n();
  const V = t.auth.verify;
  const [step, setStep] = useState<Step>({ kind: "id" });
  const [last4, setLast4] = useState("");
  const [code, setCode] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const last4Ref = useRef<HTMLInputElement>(null);
  const otpRef = useRef<HTMLInputElement>(null);
  const sendKey = useIdempotencyKey();
  const now = useNow();
  const secondsLeft = useMemo(() => (step.kind === "otp" && now ? Math.ceil((step.resendAt - now) / 1000) : null), [step, now]);
  const inviteHref = `/invite/${encodeURIComponent(token)}`;

  const sendCode = async (e?: FormEvent) => {
    e?.preventDefault();
    const digits = last4.replace(/\D/g, "");
    if (digits.length !== 4) {
      setFieldError(V.last4Error);
      last4Ref.current?.focus();
      return;
    }
    setFieldError(null);
    setFormError(null);
    setLoading(true);
    try {
      const res = await apiSend<OtpIssued>("POST", "/auth/owner/verify-id", { token, idLast4: digits }, { idempotencyKey: sendKey.get() });
      sendKey.reset();
      setCode("");
      setStep({ kind: "otp", destination: res.destination || phoneMasked, resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000, sandboxCode: res.sandboxCode ?? null });
    } catch (err) {
      if (!isApiError(err)) setFormError(t.auth.login.network);
      else if (err.code === "invitation_invalid" || err.status === 410) setStep({ kind: "invalid", message: err.title });
      else if (err.code === "id_mismatch" || err.fieldError("idLast4")) {
        sendKey.reset();
        setFieldError(err.fieldError("idLast4") ?? err.title);
        last4Ref.current?.focus();
      } else if (err.code === "offline") setFormError(t.auth.login.offline);
      else setFormError(err.title || t.auth.login.network);
    } finally {
      setLoading(false);
    }
  };

  const verify = async (e: FormEvent) => {
    e.preventDefault();
    if (code.length !== 6) {
      setFormError(t.auth.mfa.codeRequired);
      otpRef.current?.focus();
      return;
    }
    setFormError(null);
    setLoading(true);
    try {
      const res = await apiSend<{ next: string }>("POST", "/auth/owner/verify-otp", { token, code });
      hardNavigate(res.next, "/owner");
    } catch (err) {
      setLoading(false);
      setCode("");
      if (!isApiError(err)) setFormError(t.auth.login.network);
      else if (err.code === "otp_invalid") {
        const left = Number(err.reasons?.[0]);
        setFormError(Number.isFinite(left) ? t.auth.mfa.wrongCode(left) : err.title);
      } else if (err.code === "otp_exhausted" || err.status === 423) setStep({ kind: "locked", message: err.title });
      else if (err.code === "otp_expired") setFormError(t.auth.mfa.expired);
      else if (err.status === 401) {
        setStep({ kind: "id" });
        setFormError(err.title || t.auth.mfa.sessionExpired);
      } else setFormError(err.title || t.auth.login.network);
      otpRef.current?.focus();
    }
  };

  const top = (sub: string, back: { href?: string; onClick?: () => void }) => (
    <DebtorTop title={V.title} sub={sub} back={back} showBell={false} />
  );

  if (step.kind === "locked" || step.kind === "invalid") {
    return (
      <Frame top={top(V.step2, { href: inviteHref })}>
        <div role="alert" className="flex flex-col gap-4 pt-4">
          <span className="flex size-14 items-center justify-center rounded-full bg-warn-bg text-warn">
            <Icon name={step.kind === "locked" ? "timer" : "link_off"} size={30} />
          </span>
          <h1 className="m-0 text-26 leading-[38px] font-bold">{step.kind === "locked" ? V.lockedTitle : t.auth.invite.expiredTitle}</h1>
          <p className="m-0 text-17 leading-[29px]">{step.message}</p>
          <Button variant="secondary" size="xl" fullWidth href={inviteHref}>
            {t.common.back}
          </Button>
        </div>
      </Frame>
    );
  }

  if (step.kind === "otp") {
    const ready = secondsLeft !== null && secondsLeft <= 0;
    return (
      <Frame top={top(V.step2, { onClick: () => setStep({ kind: "id" }) })}>
        <form noValidate onSubmit={verify} aria-labelledby="otp-title" className="flex flex-1 flex-col gap-[18px]">
          <h1 id="otp-title" className="m-0 text-24 leading-9 font-bold">
            {V.otpTitle}
          </h1>
          <p className="m-0 text-18 leading-[30px]">
            {V.otpBodyLead}{" "}
            <bdi dir="ltr" className="font-mono">
              {step.destination}
            </bdi>
            .
          </p>
          {step.sandboxCode ? <SandboxCodeBox title={t.sandbox.title} code={step.sandboxCode} note={t.sandbox.note} /> : null}
          <OtpInput ref={otpRef} label={V.otpLabel} value={code} onChange={setCode} error={Boolean(formError)} disabled={loading} autoFocus boxHeight={56} describedBy="owner-otp-hint" />
          <span id="owner-otp-hint" aria-live="polite" className={cn("flex items-center gap-1 text-14", formError ? "text-err" : "text-muted")}>
            {formError ? (
              <>
                <Icon name="error" size={16} />
                {formError}
              </>
            ) : !ready && secondsLeft !== null ? (
              <>
                <Icon name="schedule" size={16} />
                {V.resendIn(formatCountdown(secondsLeft))}
              </>
            ) : null}
          </span>
          {ready ? (
            <Button variant="text" size="lg" onClick={() => sendCode()} loading={loading} className="self-start text-16">
              {V.resend}
            </Button>
          ) : null}
          <Button type="submit" size="xl" fullWidth loading={loading} loadingLabel={V.verifying} className="mt-auto">
            {V.verify}
          </Button>
          <Button variant="text" size="lg" onClick={() => setStep({ kind: "id" })} className="self-center text-15">
            {V.changeLast4}
          </Button>
        </form>
      </Frame>
    );
  }

  return (
    <Frame top={top(V.step1, { href: inviteHref })}>
      <form noValidate onSubmit={sendCode} aria-labelledby="verify-title" className="flex flex-1 flex-col gap-[18px]">
        <h1 id="verify-title" className="sr-only">
          {V.title}
        </h1>
        <p className="m-0 text-18 leading-[30px]">{V.intro}</p>
        <Button variant="secondary" size="xl" fullWidth icon="badge" disabled={nationalIdState !== "enabled"} aria-describedby="nid-caption" className="min-h-14 text-17">
          {V.nationalId}
        </Button>
        <span id="nid-caption" className="-mt-2 flex flex-col items-center gap-1.5 text-center text-13 text-muted">
          {V.nationalIdCaption}
          <IntegrationStateTag state={nationalIdState} />
        </span>
        <div className="flex items-center gap-2.5 text-14 text-muted" aria-hidden="true">
          <span className="h-px flex-1 bg-line" />
          {t.common.or}
          <span className="h-px flex-1 bg-line" />
        </div>
        {formError ? <Alert tone="err">{formError}</Alert> : null}
        <TextField
          ref={last4Ref}
          label={V.last4}
          size="xl"
          ltr
          mono
          inputMode="numeric"
          autoComplete="off"
          maxLength={4}
          pattern="[0-9]*"
          value={last4}
          onChange={(e) => setLast4(e.target.value.replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d))).replace(/\D/g, "").slice(0, 4))}
          error={fieldError}
          className="text-20 font-semibold tracking-[4px]"
        />
        <div className="flex flex-col gap-2">
          <span className="text-16 font-semibold" id="phone-label">
            {V.phoneLabel}
          </span>
          <div aria-labelledby="phone-label" role="group" className="flex h-[54px] items-center rounded-[8px] border border-line bg-subtle px-3.5">
            <bdi dir="ltr" className="font-mono text-17 font-medium">
              {phoneMasked || "—"}
            </bdi>
          </div>
          <span className="text-14 text-muted">
            {V.wrongNumber} <a href={inviteHref}>{V.contactBank}</a>
          </span>
        </div>
        <Button type="submit" size="xl" fullWidth loading={loading} loadingLabel={V.sending} className="mt-auto">
          {V.send}
        </Button>
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
