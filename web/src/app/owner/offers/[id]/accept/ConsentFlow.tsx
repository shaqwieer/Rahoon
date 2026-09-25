"use client";

import { useMemo, useRef, useState, type FormEvent } from "react";
import { OwnerHeading } from "@/components/owner/OwnerPage";
import type { OwnerShellData } from "@/components/owner/server";
import type { ConsentOtpIssued, ConsentResult, OwnerOffer } from "@/components/owner/types";
import { TouchLink } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { OwnerShell } from "@/components/shell/OwnerShell";
import { Alert, Button, Checkbox, DateText, Dialog, Icon, OtpInput, SandboxCodeBox } from "@/components/ui";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { formatCountdown } from "@/lib/format";
import { useNow } from "@/lib/hooks";
import { useI18n } from "@/lib/i18n/client";
import { lateText, OfferHero, OfferTerms } from "../terms";

const ACKS = ["terms_read", "voluntary"] as const;

interface OtpState {
  destination: string;
  resendAt: number;
  sandboxCode: string | null;
}

/**
 * D09 — informed consent: summary, two required acknowledgements, SMS code, then «أوافق على العرض».
 * The result is a consent record confirmed by OTP — explicitly not a licensed electronic signature.
 */
export function ConsentFlow({ offer, shell }: { offer: OwnerOffer; shell: OwnerShellData }) {
  const c = useOwnerCopy();
  const K = c.consent;
  const { t, locale, numerals } = useI18n();
  const offerHref = `/owner/offers/${offer.id}`;
  const [acks, setAcks] = useState({ terms_read: false, voluntary: false });
  const [ackError, setAckError] = useState(false);
  const [otp, setOtp] = useState<OtpState | null>(null);
  const [code, setCode] = useState("");
  const [codeError, setCodeError] = useState<string | null>(null);
  const [closed, setClosed] = useState<string | null>(null);
  const [result, setResult] = useState<ConsentResult | null>(null);
  const [termsOpen, setTermsOpen] = useState(false);
  const send = useSubmit();
  const agree = useSubmit();
  const otpRef = useRef<HTMLInputElement>(null);
  const doneRef = useRef<HTMLHeadingElement>(null);
  const now = useNow();
  const secondsLeft = useMemo(() => (otp && now ? Math.ceil((otp.resendAt - now) / 1000) : null), [otp, now]);
  const bothAcks = acks.terms_read && acks.voluntary;
  const canAgree = bothAcks && otp !== null && code.length === 6;

  const offerClosed = (e: unknown) => isApiError(e) && (e.code === "offer_closed" || e.code === "offer_expired");

  const sendCode = async () => {
    setCodeError(null);
    const res = await send.run(
      (k) => apiSend<ConsentOtpIssued>("POST", `/owner/offers/${offer.id}/consent/otp`, undefined, { idempotencyKey: k }),
      (e) => {
        if (offerClosed(e) && isApiError(e)) {
          setClosed(e.title);
          return null;
        }
        return undefined;
      },
    );
    if (res.ok) {
      setCode("");
      setOtp({ destination: res.data.destination, resendAt: Date.now() + (res.data.resendInSeconds ?? 60) * 1000, sandboxCode: res.data.sandboxCode ?? null });
      requestAnimationFrame(() => otpRef.current?.focus());
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!bothAcks) {
      setAckError(true);
      return;
    }
    if (code.length !== 6) {
      setCodeError(K.codeRequired);
      otpRef.current?.focus();
      return;
    }
    setCodeError(null);
    const res = await agree.run(
      (k) => apiSend<ConsentResult>("POST", `/owner/offers/${offer.id}/consent`, { acknowledgements: [...ACKS], code }, { idempotencyKey: k }),
      (err) => {
        if (!isApiError(err)) return undefined;
        // Both OTP failures are 401: branch on the code first.
        if (err.code === "otp_invalid") {
          const left = Number(err.reasons?.[0]);
          setCode("");
          setCodeError(Number.isFinite(left) ? K.wrongCode(left) : err.title);
          otpRef.current?.focus();
          return null;
        }
        if (err.code === "otp_exhausted" || err.code === "otp_expired") {
          setCode("");
          setOtp(null);
          return err.title;
        }
        if (offerClosed(err)) {
          setClosed(err.title);
          return null;
        }
        if (err.fieldError("acknowledgements")) {
          setAckError(true);
          return null;
        }
        return undefined;
      },
    );
    if (res.ok) {
      setResult(res.data);
      window.scrollTo({ top: 0 });
      requestAnimationFrame(() => doneRef.current?.focus());
    }
  };

  const shellProps = { userLabel: shell.userLabel, unread: shell.unread, numerals: shell.numerals, hideNav: true, active: "options" as const };

  if (result) {
    return (
      <OwnerShell {...shellProps} title={K.doneTitle}>
        <div role="status" className="mx-auto flex w-full max-w-[560px] flex-col gap-4 pt-3.5">
          <span className="flex size-[60px] items-center justify-center rounded-full bg-ok-bg">
            <Icon name="check_circle" size={34} className="text-ok" />
          </span>
          <h1 ref={doneRef} tabIndex={-1} className="m-0 text-26 leading-[38px] font-bold outline-none">
            {K.thanks}
          </h1>
          <ServerText as="p" text={result.message} className="m-0 text-18 leading-[30px]" />
          <div className="flex flex-col gap-1 rounded-[12px] border border-line bg-white px-4 py-3.5 text-15 leading-6">
            <span className="text-muted">{K.reference}</span>
            <bdi dir="ltr" className="self-start font-mono text-16 font-semibold">
              {result.agreementRef}
            </bdi>
            <span className="text-muted">
              <DateText value={result.recordedAt} mode="datetime" /> · <DateText value={result.recordedAt} mode="hijri" />
            </span>
          </div>
          <p className="m-0 flex items-start gap-2 text-14 leading-[22px] text-muted">
            <Icon name="verified_user" size={18} />
            {K.notSignature}
          </p>
          <Button href="/owner/agreement" variant="secondary" size="lg" icon="download" className="min-h-[52px] rounded-[8px]">
            {K.downloadCopy}
          </Button>
          <Button href="/owner" size="lg" className="min-h-[52px] rounded-[8px]">
            {c.backHome}
          </Button>
        </div>
      </OwnerShell>
    );
  }

  const ready = secondsLeft !== null && secondsLeft <= 0;
  return (
    <OwnerShell {...shellProps} title={K.title} back={{ href: offerHref }}>
      <form noValidate onSubmit={submit} className="mx-auto flex w-full max-w-[560px] flex-col gap-3.5">
        <OwnerHeading title={K.title} backHref={offerHref} backLabel={K.backToOffer} />
        {closed ? (
          <div role="alert" className="flex flex-col gap-1">
            <Alert tone="warn" role="none">
              {closed}
            </Alert>
            <TouchLink href={offerHref}>{K.backToOffer}</TouchLink>
          </div>
        ) : null}

        <section aria-labelledby="agree-summary" className="flex flex-col gap-2 rounded-[12px] border border-line bg-white p-4 text-16 leading-[26px]">
          <h2 id="agree-summary" className="m-0 text-17 font-bold">
            {K.youAgree}
          </h2>
          <ul className="m-0 flex list-disc flex-col gap-1 ps-5">
            {offer.summary.map((s, i) => (
              <li key={i}>
                <ServerText text={s} />
              </li>
            ))}
          </ul>
          <Button variant="text" size="lg" className="self-start px-0 text-16" onClick={() => setTermsOpen(true)}>
            {K.fullTerms}
          </Button>
        </section>

        <fieldset className="m-0 flex flex-col gap-3 border-0 p-0" aria-describedby={ackError && !bothAcks ? "ack-error" : undefined}>
          <legend className="sr-only">{K.youAgree}</legend>
          <Checkbox
            boxSize={24}
            label={K.ackTerms}
            checked={acks.terms_read}
            onChange={(e) => {
              setAcks({ ...acks, terms_read: e.target.checked });
              agree.resetKey();
            }}
          />
          <Checkbox
            boxSize={24}
            label={K.ackVoluntary}
            checked={acks.voluntary}
            onChange={(e) => {
              setAcks({ ...acks, voluntary: e.target.checked });
              agree.resetKey();
            }}
          />
          {ackError && !bothAcks ? (
            <span id="ack-error" role="alert" className="flex items-center gap-1 text-14 text-err">
              <Icon name="error" size={16} />
              {K.ackRequired}
            </span>
          ) : null}
        </fieldset>

        {otp ? (
          <div className="flex flex-col gap-2">
            <p className="m-0 text-15 text-charcoal">
              {K.codeSentTo}{" "}
              <bdi dir="ltr" className="font-mono">
                {otp.destination || offer.phoneMasked}
              </bdi>
            </p>
            {otp.sandboxCode ? <SandboxCodeBox title={t.sandbox.title} code={otp.sandboxCode} note={t.sandbox.note} /> : null}
            <OtpInput
              ref={otpRef}
              label={K.codeLabel}
              value={code}
              onChange={(v) => {
                setCode(v);
                setCodeError(null);
                agree.resetKey();
              }}
              error={Boolean(codeError)}
              disabled={agree.busy}
              boxHeight={52}
              describedBy="consent-otp-hint"
            />
            <span id="consent-otp-hint" aria-live="polite" className={cn("flex items-center gap-1 text-14", codeError ? "text-err" : "text-muted")}>
              {codeError ? (
                <>
                  <Icon name="error" size={16} />
                  {codeError}
                </>
              ) : !ready && secondsLeft !== null ? (
                <>
                  <Icon name="schedule" size={16} />
                  {K.resendIn(formatCountdown(secondsLeft))}
                </>
              ) : null}
            </span>
            {ready ? (
              <Button variant="text" size="lg" className="self-start px-0 text-15" loading={send.busy} onClick={() => void sendCode()}>
                {K.resend}
              </Button>
            ) : null}
          </div>
        ) : null}

        <div aria-live="assertive">
          {send.error || agree.error ? (
            <Alert tone="err" role="none">
              {agree.error ?? send.error}
            </Alert>
          ) : null}
        </div>

        <p className="m-0 flex items-start gap-2 text-14 leading-[22px] text-muted">
          <Icon name="verified_user" size={18} />
          {K.notSignature}
        </p>

        {otp ? (
          <Button type="submit" size="xl" fullWidth softDisabled={!canAgree || Boolean(closed)} loading={agree.busy} loadingLabel={K.recording} className="text-17">
            {K.agree}
          </Button>
        ) : (
          <Button size="xl" fullWidth disabled={Boolean(closed)} loading={send.busy} loadingLabel={c.sending} onClick={() => void sendCode()} className="text-17">
            {K.sendCode}
          </Button>
        )}
      </form>

      <Dialog open={termsOpen} onClose={() => setTermsOpen(false)} title={K.fullTermsTitle} size="md">
        <div className="flex flex-col gap-3 p-5">
          <OfferHero offer={offer} c={c} />
          <OfferTerms offer={offer} c={c} />
          <h3 className="m-0 text-16 font-semibold">{c.offer.whatIfLate}</h3>
          <p className="m-0 text-16 leading-[26px]">{lateText(offer, c, { locale, numerals })}</p>
          <ul className="m-0 flex list-disc flex-col gap-1 ps-5 text-15">
            {offer.summary.map((s, i) => (
              <li key={i}>
                <ServerText text={s} />
              </li>
            ))}
          </ul>
        </div>
      </Dialog>
    </OwnerShell>
  );
}
