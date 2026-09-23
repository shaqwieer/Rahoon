"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";
import { LocaleSwitch } from "@/components/shell/LocaleSwitch";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { IconButton } from "@/components/ui/IconButton";
import { Logo } from "@/components/ui/Logo";
import { apiSend, isApiError } from "@/lib/api/client";
import { writeSessionValue } from "@/lib/hooks";
import { useI18n } from "@/lib/i18n/client";
import { MFA_STORAGE_KEY, type MfaHandoff } from "../mfa-handoff";

interface LoginResponse {
  mfaRequired: boolean;
  factor: string;
  destination: string;
  resendInSeconds: number;
  sandboxCode?: string | null;
}

type FormError =
  | { kind: "credentials"; remaining?: number }
  | { kind: "locked"; minutes: number }
  | { kind: "message"; text: string };

export function LoginForm({ next }: { next: string }) {
  const { t, locale } = useI18n();
  const L = t.auth.login;
  const router = useRouter();
  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [error, setError] = useState<FormError | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const fe: typeof fieldErrors = {};
    if (!email.trim()) fe.email = L.emailRequired;
    if (!password) fe.password = L.passwordRequired;
    setFieldErrors(fe);
    setError(null);
    if (fe.email || fe.password) {
      (fe.email ? emailRef : passwordRef).current?.focus();
      return;
    }
    setLoading(true);
    try {
      const res = await apiSend<LoginResponse>("POST", "/auth/login", { email: email.trim(), password });
      const handoff: MfaHandoff = {
        destination: res.destination,
        resendAt: Date.now() + (res.resendInSeconds ?? 60) * 1000,
        sandboxCode: res.sandboxCode ?? null,
      };
      writeSessionValue(MFA_STORAGE_KEY, handoff);
      router.push(next ? `/login/mfa?next=${encodeURIComponent(next)}` : "/login/mfa");
    } catch (err) {
      setLoading(false);
      setPassword("");
      if (isApiError(err)) {
        if (err.status === 423) setError({ kind: "locked", minutes: err.minutes ?? 15 });
        else if (err.status === 401) setError({ kind: "credentials", remaining: err.remainingAttempts });
        else if (err.code === "offline") setError({ kind: "message", text: L.offline });
        else if (err.code === "network") setError({ kind: "message", text: L.network });
        else setError({ kind: "message", text: err.title || L.network });
      } else {
        setError({ kind: "message", text: L.network });
      }
      passwordRef.current?.focus();
    }
  };

  const alert =
    error?.kind === "credentials" ? (
      <Alert tone="err" role="alert">
        <strong className="text-err">{L.genericError}</strong> {error.remaining !== undefined ? L.remaining(error.remaining) : null}
      </Alert>
    ) : error?.kind === "locked" ? (
      <Alert tone="warn" role="alert" icon="timer" title={L.lockedTitle}>
        {L.lockedBody(error.minutes)}
      </Alert>
    ) : error?.kind === "message" ? (
      <Alert tone="err" role="alert">
        {error.text}
      </Alert>
    ) : null;

  return (
    <form noValidate aria-labelledby="login-title" onSubmit={onSubmit} className="flex flex-col gap-[18px]">
      <Logo variant="horizontal" width={176} alt={t.brand.name} priority className="mb-3" />
      <h1 id="login-title" className="m-0 text-32 leading-[44px] font-bold">
        {L.title}
      </h1>
      <p className="m-0 text-15 leading-6 text-muted">{L.sub}</p>

      <TextField
        ref={emailRef}
        label={L.email}
        type="email"
        name="email"
        autoComplete="username"
        inputMode="email"
        ltr
        size="lg"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        error={fieldErrors.email}
        disabled={loading}
      />

      {/* The design places the credential error between the two fields. */}
      {alert}

      <TextField
        ref={passwordRef}
        label={L.password}
        labelAside={
          <Link href="#" className="text-14 font-semibold">
            {L.forgot}
          </Link>
        }
        type={showPassword ? "text" : "password"}
        name="password"
        autoComplete="current-password"
        size="lg"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={fieldErrors.password}
        invalid={error?.kind === "credentials"}
        disabled={loading}
        endAdornment={
          <IconButton
            label={showPassword ? L.hidePassword : L.showPassword}
            icon={showPassword ? "visibility_off" : "visibility"}
            size={48}
            iconSize={20}
            className="rounded-none text-muted"
            aria-pressed={showPassword}
            onClick={() => setShowPassword((v) => !v)}
          />
        }
      />

      <Button type="submit" size="lg" fullWidth loading={loading} loadingLabel={L.submitting}>
        {L.submit}
      </Button>

      <div className="flex gap-2.5 border-t border-line pt-4 text-14 leading-[22px]">
        <Icon name="person" size={20} className="text-muted" />
        <span>
          {L.ownerNoteLead} <strong>{L.ownerNoteLink}</strong> {L.ownerNoteTail}{" "}
          {locale === "ar" ? <Link href="#">{L.ownerVerifyLink}</Link> : null}
        </span>
      </div>
      <div className="flex flex-wrap items-center gap-4 text-13 text-muted">
        <LocaleSwitch variant="link" className="min-h-0 px-0 text-13 font-normal text-rust underline" />
        <Link href="#">{L.privacy}</Link>
        <Link href="#">{L.help}</Link>
      </div>
    </form>
  );
}
