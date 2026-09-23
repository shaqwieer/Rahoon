"use client";

import { useState, type ReactNode } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { useI18n } from "@/lib/i18n/client";

/** D01a frame: 64px white header with the horizontal logo, then a single reading column (mobile-first). */
export function OwnerFrame({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="flex min-h-dvh flex-col bg-warm">
      <SkipLink />
      <header className="flex h-16 items-center border-b border-divider bg-white px-5">
        <Logo variant="horizontal" width={172} alt={t.brand.name} priority />
      </header>
      <main id="main" tabIndex={-1} className="mx-auto flex w-full max-w-[480px] flex-1 flex-col gap-[18px] px-[22px] py-7 outline-none">
        {children}
      </main>
    </div>
  );
}

/** Expired / used / invalid invitation notice (S12 mobile pattern). */
export function InviteNotice({
  icon,
  tone,
  title,
  body,
  primary,
  secondary,
}: {
  icon: string;
  tone: "warn" | "info" | "err";
  title: string;
  body: string;
  primary?: { label: string; href?: string; revealNote?: string };
  secondary?: { label: string; revealNote?: string };
}) {
  const bg = { warn: "bg-warn-bg text-warn", info: "bg-info-bg text-info", err: "bg-err-bg text-err" }[tone];
  return (
    <div role="status" className="flex flex-col gap-4 pt-5">
      <span className={`flex size-14 items-center justify-center rounded-full ${bg}`}>
        <Icon name={icon} size={30} />
      </span>
      <h1 className="m-0 text-26 leading-[38px] font-bold">{title}</h1>
      <p className="m-0 text-17 leading-[29px] text-charcoal">{body}</p>
      {primary ? (
        primary.href ? (
          <Button size="xl" fullWidth href={primary.href} className="min-h-[52px] rounded-sm text-16">
            {primary.label}
          </Button>
        ) : (
          <RevealAction label={primary.label} note={primary.revealNote ?? ""} variant="primary" />
        )
      ) : null}
      {secondary ? <RevealAction label={secondary.label} note={secondary.revealNote ?? ""} variant="text" /> : null}
    </div>
  );
}

/** A button that explains the next step inline (used where the API offers no self-service endpoint yet). */
export function RevealAction({ label, note, variant }: { label: string; note: string; variant: "primary" | "text" }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="flex flex-col gap-2">
      <Button
        variant={variant}
        size={variant === "primary" ? "xl" : "lg"}
        fullWidth={variant === "primary"}
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
        className={variant === "primary" ? "min-h-[52px] rounded-sm text-16" : "self-start text-15 no-underline"}
      >
        {label}
      </Button>
      {open ? (
        <Alert tone="info" role="status" compact>
          {note}
        </Alert>
      ) : null}
    </div>
  );
}
