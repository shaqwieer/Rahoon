"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { IconButton } from "@/components/ui/IconButton";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { Numerals } from "@/lib/format";
import { I18nProvider, useI18n } from "@/lib/i18n/client";
import { LocaleSwitch } from "./LocaleSwitch";
import { activeKey, OWNER_NAV, type OwnerNavKey } from "./nav";
import { UserMenu } from "./UserMenu";

/* ───────── DebtorTop (60px) ───────── */

export interface DebtorTopProps {
  title: ReactNode;
  sub?: ReactNode;
  /** Shows a back button (arrow_forward in RTL, mirrored in LTR) instead of the symbol logo. */
  back?: { href?: string; onClick?: () => void } | boolean;
  unread?: number;
  /** Bell target (owner notifications). */
  bellHref?: string;
  /** Hide the bell before sign-in (invitation / verification steps). */
  showBell?: boolean;
  /** Extra trailing control (e.g. account menu). */
  trailing?: ReactNode;
  className?: string;
}

/** DebtorTop.dc: symbol or back · title/sub · bell with dot. Touch targets 44px. */
export function DebtorTop({ title, sub, back, unread = 0, bellHref = "/owner/notifications", showBell = true, trailing, className }: DebtorTopProps) {
  const { t } = useI18n();
  const router = useRouter();
  const backDef = back === true ? {} : back || null;
  return (
    <header className={cn("sticky top-0 z-30 flex h-[60px] items-center gap-1.5 border-b border-divider bg-white ps-3 pe-2", className)}>
      {backDef ? (
        backDef.href ? (
          <IconButton href={backDef.href} label={t.common.back} icon="arrow_forward" mirror size={44} iconSize={24} />
        ) : (
          <IconButton label={t.common.back} icon="arrow_forward" mirror size={44} iconSize={24} onClick={backDef.onClick ?? (() => router.back())} />
        )
      ) : (
        <Logo variant="symbol" width={28} alt={t.brand.name} className="mx-1" />
      )}
      <div className="flex min-w-0 flex-1 flex-col">
        <strong className="truncate text-17 leading-6">{title}</strong>
        {sub ? <span className="truncate text-12 leading-4 text-muted">{sub}</span> : null}
      </div>
      {showBell ? <IconButton href={bellHref} label={t.shell.notificationsNew(unread)} icon="notifications" size={44} iconSize={24} dot={unread > 0} /> : null}
      {trailing}
    </header>
  );
}

/* ───────── DebtorNav (68px, 5 tabs) ───────── */

export function DebtorNav({ active, className }: { active?: OwnerNavKey | null; className?: string }) {
  const { t } = useI18n();
  const pathname = usePathname();
  const current = active ?? activeKey(OWNER_NAV, pathname);
  return (
    <nav aria-label={t.nav.owner.label} className={cn("grid h-[68px] grid-cols-5 border-t border-line bg-white", className)}>
      {OWNER_NAV.map((i) => {
        const on = i.key === current;
        return (
          <Link
            key={i.key}
            href={i.href}
            aria-current={on ? "page" : undefined}
            className={cn(
              "flex min-h-12 flex-col items-center justify-center gap-[3px] text-12 no-underline",
              on ? "font-bold text-rust-700 shadow-[inset_0_3px_0_#F4633A] hover:text-rust-700" : "font-medium text-muted hover:text-ink",
            )}
          >
            <Icon name={i.icon} size={24} />
            {t.nav.owner[i.key]}
          </Link>
        );
      })}
    </nav>
  );
}

/* ───────── OwnerDesktopHeader (≥1024, B6 D02 desktop) ───────── */

export function OwnerDesktopHeader({ userLabel, active, unread = 0, bellHref = "/owner/notifications" }: { userLabel: string; active?: OwnerNavKey | null; unread?: number; bellHref?: string }) {
  const { t } = useI18n();
  const pathname = usePathname();
  const current = active ?? activeKey(OWNER_NAV, pathname);
  return (
    <header className="sticky top-0 z-30 flex h-[72px] items-center gap-8 border-b border-line bg-white px-16">
      <Link href="/owner" className="inline-flex rounded-xs">
        <Logo variant="horizontal" width={176} alt={t.brand.name} priority />
      </Link>
      <nav aria-label={t.nav.owner.label}>
        <ul className="m-0 flex list-none gap-1 p-0 text-16">
          {OWNER_NAV.map((i) => {
            const on = i.key === current;
            return (
              <li key={i.key}>
                <Link
                  href={i.href}
                  aria-current={on ? "page" : undefined}
                  className={cn("inline-flex min-h-11 items-center rounded-sm px-3.5 no-underline", on ? "bg-rust-50 font-bold text-rust-700 hover:text-rust-700" : "text-ink hover:bg-subtle hover:text-ink")}
                >
                  {t.nav.owner[i.key]}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>
      <div className="ms-auto flex items-center gap-2">
        <IconButton href={bellHref} label={t.shell.notificationsNew(unread)} icon="notifications" size={44} iconSize={24} dot={unread > 0} />
        <LocaleSwitch />
        <UserMenu name={userLabel} initials={userLabel.slice(0, 1)} profileHref={null} appearance="name" />
      </div>
    </header>
  );
}

/* ───────── OwnerShell ───────── */

export interface OwnerShellProps {
  /** DebtorTop title (default greeting set by the layout). */
  title: ReactNode;
  sub?: ReactNode;
  /** «عبدالله م. · مصرف الأفق» (desktop header). */
  userLabel: string;
  unread?: number;
  back?: DebtorTopProps["back"];
  /** Focused flows (offer, consent, messages) hide the bottom nav. */
  hideNav?: boolean;
  active?: OwnerNavKey | null;
  numerals?: Numerals;
  /** Bell target on mobile and desktop (default /owner/notifications). */
  bellHref?: string;
  children: ReactNode;
}

/**
 * Owner (debtor) portal: mobile-first DebtorTop + content + DebtorNav; from 1024px a top header with links
 * and a centered 1040px column — no sidebar (B6 responsive rule).
 */
export function OwnerShell({ title, sub, userLabel, unread = 0, back, hideNav, active, numerals = "latn", bellHref, children }: OwnerShellProps) {
  const { locale } = useI18n();
  return (
    <I18nProvider locale={locale} numerals={numerals}>
    <div className="flex min-h-dvh flex-col bg-warm">
      <div className="print:hidden">
        <SkipLink />
      </div>
      <div className="lg:hidden print:hidden">
        <DebtorTop title={title} sub={sub} unread={unread} back={back} bellHref={bellHref} />
      </div>
      <div className="hidden lg:block print:hidden">
        <OwnerDesktopHeader userLabel={userLabel} active={active} unread={unread} bellHref={bellHref} />
      </div>
      <main
        id="main"
        tabIndex={-1}
        className={cn(
          "mx-auto w-full flex-1 px-[18px] py-[18px] text-16 outline-none md:max-w-[560px] md:px-0 lg:max-w-[1040px] lg:py-10",
          !hideNav && "pb-24 lg:pb-10",
          "print:max-w-none print:p-0",
        )}
      >
        {children}
      </main>
      {hideNav ? null : <DebtorNav active={active} className="fixed inset-x-0 bottom-0 z-30 lg:hidden print:hidden" />}
    </div>
    </I18nProvider>
  );
}
