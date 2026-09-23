"use client";

import Link from "next/link";
import { useMemo, useRef, useState, type FormEvent } from "react";
import { SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { IconButton } from "@/components/ui/IconButton";
import { OtpInput } from "@/components/ui/OtpInput";
import { useToast } from "@/components/ui/Toast";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { formatCountdown } from "@/lib/format";
import { parseJson, useNow, useSessionValue, writeSessionValue } from "@/lib/hooks";
import { useI18n } from "@/lib/i18n/client";
import { hardNavigate } from "@/lib/navigation";
import { MFA_STORAGE_KEY, type MfaHandoff } from "../../mfa-handoff";

type Phase = "enter" | "locked" | "session-expired";

/** Where to go after a successful second factor: keep the deep link unless the user must pick a workspace. */
function destinationAfterMfa(serverNext: string, next: string) {
  if (serverNext === "/select-context") return next ? `/select-context?next=${encodeURIComponent(next)}` : serverNext;
  if (serverNext === "/access-denied" || !next) return serverNext;
  return next;
}

export function MfaForm({ next }: { next: string }) {
  const { t } = useI18n();
  const M = t.auth.mfa;
  const { toast } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const handoff = parseJson<MfaHandoff>(useSessionValue(MFA_STORAGE_KEY));
  const now = useNow();
  const [code, setCode] = useState("");
  const [phase, setPhase] = useState<Phase>("enter");
  const [error, setError] = useState<string | null>(null);
  const [canResendEarly, setCanResendEarly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const hintId = "mfa-hint";

  const secondsLeft = useMemo(() => (handoff && now ? Math.ceil((handoff.resendAt - now) / 1000) : null), [handoff, now]);
  const resendReady = canResendEarly || !handoff || (secondsLeft !== null && secondsLeft <= 0);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (code.length !== 6) {
      setError(M.codeRequired);
      inputRef.current?.focus();
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await apiSend<{ next: string }>("POST", "/auth/mfa/verify", { code });
      writeSessionValue(MFA_STORAGE_KEY, null);
      hardNavigate(destinationAfterMfa(res.next, next), "/");
    } catch (err) {
      setLoading(false);
      setCode("");
      if (!isApiError(err)) {
        setError(t.auth.login.network);
      } else if (err.code === "otp_invalid") {
        const left = Number(err.reasons?.[0]);
        setError(Number.isFinite(left) ? M.wrongCode(left) : err.title);
      } else if (err.code === "locked" || err.status === 423) {
        writeSessionValue(MFA_STORAGE_KEY, null);
        setPhase("locked");
        return;
      } else if (err.code === "otp_expired") {
        setError(M.expired);
        setCanResendEarly(true);
      } else if (err.status === 401) {
        writeSessionValue(MFA_STORAGE_KEY, null);
        setPhase("session-expired");
        return;
      } else if (err.code === "offline") {
        setError(t.auth.login.offline);
      } else {
        setError(err.title || t.auth.login.network);
      }
      inputRef.current?.focus();
    }
  };

  const resend = async () => {
    setResending(true);
    try {
      const res = await apiSend<{ destination: string; resendInSeconds: number; sandboxCode?: string | null }>("POST", "/auth/mfa/resend");
      writeSessionValue(MFA_STORAGE_KEY, {
        destination: res.destination,
        resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000,
        sandboxCode: res.sandboxCode ?? null,
      } satisfies MfaHandoff);
      setCanResendEarly(false);
      setError(null);
      setCode("");
      toast({ tone: "ok", message: M.resent });
      inputRef.current?.focus();
    } catch (err) {
      if (isApiError(err) && err.status === 401) setPhase("session-expired");
      else setError(isApiError(err) && err.title ? err.title : t.auth.login.network);
    } finally {
      setResending(false);
    }
  };

  const loginHref = next ? `/login?next=${encodeURIComponent(next)}` : "/login";

  if (phase === "locked" || phase === "session-expired") {
    const locked = phase === "locked";
    return (
      <div role="alert" className="flex flex-col gap-[18px]">
        <span className={cn("flex size-12 items-center justify-center rounded-[12px]", locked ? "bg-warn-bg" : "bg-subtle")}>
          <Icon name={locked ? "timer" : "schedule"} size={26} className={locked ? "text-warn" : "text-charcoal"} />
        </span>
        <h1 className="m-0 text-26 leading-[38px] font-bold md:text-32 md:leading-[44px]">{locked ? M.lockedTitle : M.sessionExpired}</h1>
        {locked ? <p className="m-0 text-16 leading-[26px] text-charcoal">{M.lockedBody}</p> : null}
        <Button variant="secondary" size="lg" fullWidth href={loginHref}>
          {M.backToLogin}
        </Button>
        <Link href="#" className="flex min-h-11 items-center text-15 font-semibold">
          {M.contactSupport}
        </Link>
      </div>
    );
  }

  return (
    <form noValidate aria-labelledby="mfa-title" onSubmit={onSubmit} className="flex flex-col gap-[18px]">
      <IconButton href={loginHref} label={t.common.back} icon="arrow_forward" mirror size={44} iconSize={22} className="-ms-2.5 -mb-2" />
      <span className="flex size-12 items-center justify-center rounded-[12px] bg-subtle">
        <Icon name="sms" size={26} className="text-charcoal" />
      </span>
      <h1 id="mfa-title" className="m-0 text-26 leading-[38px] font-bold md:text-32 md:leading-[44px]">
        {M.title}
      </h1>
      <p className="m-0 text-16 leading-[26px] text-charcoal">
        {handoff ? (
          <>
            {M.bodyLead}{" "}
            <bdi dir="ltr" className="font-mono">
              {handoff.destination}
            </bdi>
            .
          </>
        ) : (
          M.bodyGeneric
        )}
      </p>

      {handoff?.sandboxCode ? <SandboxCodeBox title={t.sandbox.title} code={handoff.sandboxCode} note={t.sandbox.note} /> : null}

      <OtpInput ref={inputRef} value={code} onChange={setCode} error={Boolean(error)} disabled={loading} autoFocus describedBy={hintId} />

      <span id={hintId} aria-live="polite" className={cn("flex items-center gap-1 text-13", error ? "text-err" : "text-muted")}>
        {error ? (
          <>
            <Icon name="error" size={16} />
            {error}
          </>
        ) : !resendReady && secondsLeft !== null ? (
          <>
            <Icon name="schedule" size={16} />
            {M.resendIn(formatCountdown(secondsLeft))}
          </>
        ) : null}
      </span>

      <Button type="submit" size="lg" fullWidth loading={loading} loadingLabel={M.submitting}>
        {M.submit}
      </Button>

      {resendReady ? (
        <Button variant="text" size="lg" onClick={resend} loading={resending} className="self-start">
          {M.resend}
        </Button>
      ) : null}
      <div className="flex flex-col gap-0.5">
        <Button variant="text" size="lg" disabled className="self-start no-underline">
          {M.altFactor}
        </Button>
        <span className="ps-2 text-12 text-muted">{M.altFactorUnavailable}</span>
      </div>
    </form>
  );
}
