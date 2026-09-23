"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState, type ReactNode } from "react";
import { Drawer } from "@/components/ui/Dialog";
import { Icon } from "@/components/ui/Icon";
import { IconButton } from "@/components/ui/IconButton";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { cn } from "@/lib/cn";
import { I18nProvider, useI18n } from "@/lib/i18n/client";
import { CommandPalette, useCommandPaletteShortcut } from "./CommandPalette";
import { LenderSidebar } from "./LenderSidebar";
import { LenderTopbar } from "./LenderTopbar";
import { LocaleSwitch } from "./LocaleSwitch";
import { activeKey, LENDER_NAV, lenderCrumbs, visibleNav, type Crumb, type LenderNavKey } from "./nav";
import { useSessionActions } from "./session";
import type { ShellUser } from "./types";

export interface LenderShellProps {
  user: ShellUser;
  children: ReactNode;
  /** Override the URL-derived breadcrumb. */
  crumbs?: Crumb[];
  badges?: Partial<Record<LenderNavKey, number>>;
}

/**
 * Lender workspace shell: skip link → sidebar → topbar → <main id="main"> (padding 28/40/40).
 * <768: 56px top bar (symbol, org, search, bell) + 64px bottom nav (المحفظة، الحالات، مهامي، المزيد).
 */
export function LenderShell({ user, children, crumbs, badges }: LenderShellProps) {
  const { locale } = useI18n();
  return (
    <I18nProvider locale={locale} numerals={user.numerals}>
      <LenderShellInner user={user} crumbs={crumbs} badges={badges}>
        {children}
      </LenderShellInner>
    </I18nProvider>
  );
}

function LenderShellInner({ user, children, crumbs, badges }: LenderShellProps) {
  const { t } = useI18n();
  const pathname = usePathname();
  const [searchOpen, setSearchOpen] = useState(false);
  useCommandPaletteShortcut(() => setSearchOpen(true));

  return (
    <div className="flex min-h-dvh">
      <SkipLink />
      <LenderSidebar user={user} badges={badges} />
      <div className="flex min-w-0 flex-1 flex-col">
        <LenderTopbar user={user} crumbs={crumbs ?? lenderCrumbs(pathname, t)} onOpenSearch={() => setSearchOpen(true)} />
        <MobileTopBar user={user} />
        <main id="main" tabIndex={-1} className="flex-1 px-4 pt-4 pb-24 outline-none md:px-10 md:pt-7 md:pb-10">
          {children}
        </main>
        <MobileBottomNav user={user} badges={badges} />
      </div>
      <CommandPalette open={searchOpen} onClose={() => setSearchOpen(false)} orgName={user.orgName} />
    </div>
  );
}

function MobileTopBar({ user }: { user: ShellUser }) {
  const { t } = useI18n();
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-2.5 border-b border-line bg-white ps-4 pe-2 md:hidden">
      <Link href="/portfolio" className="inline-flex rounded-xs">
        <Logo variant="symbol" width={28} alt={t.brand.name} priority />
      </Link>
      <span className="min-w-0 flex-1 truncate text-14 font-semibold">
        {user.orgName} <span className="font-normal text-muted">· {user.roleName}</span>
      </span>
      <IconButton href="/search" label={t.common.search} icon="search" size={44} iconSize={22} />
      <IconButton href="/notifications" label={t.shell.notificationsUnread(user.unread)} icon="notifications" size={44} iconSize={22} badge={user.unread} />
    </header>
  );
}

function MobileBottomNav({ user, badges }: { user: ShellUser; badges?: Partial<Record<LenderNavKey, number>> }) {
  const { t } = useI18n();
  const pathname = usePathname();
  const [moreOpen, setMoreOpen] = useState(false);
  const { logout, busy } = useSessionActions();
  const items = visibleNav(LENDER_NAV, user.permissions);
  const primary = items.filter((i) => i.key === "portfolio" || i.key === "cases" || i.key === "tasks").slice(0, 3);
  const rest = items.filter((i) => !primary.includes(i));
  const current = activeKey(items, pathname);
  const moreActive = current !== null && rest.some((i) => i.key === current);

  const tab = (active: boolean) =>
    cn(
      "flex min-h-11 flex-col items-center justify-center gap-0.5 text-11 no-underline",
      active ? "font-bold text-rust-700 hover:text-rust-700" : "text-muted hover:text-ink",
    );

  return (
    <>
      <nav aria-label={t.nav.lender.mobileLabel} className="fixed inset-x-0 bottom-0 z-30 grid h-16 grid-cols-4 border-t border-line bg-white md:hidden">
        {primary.map((i) => (
          <Link key={i.key} href={i.href} aria-current={i.key === current ? "page" : undefined} className={tab(i.key === current)}>
            <Icon name={i.icon} size={22} />
            {t.nav.lender[i.key]}
          </Link>
        ))}
        <button type="button" aria-haspopup="dialog" aria-expanded={moreOpen} onClick={() => setMoreOpen(true)} className={cn(tab(moreActive), "bg-transparent")}>
          <Icon name="menu" size={22} />
          {t.nav.lender.more}
        </button>
      </nav>
      <Drawer open={moreOpen} onClose={() => setMoreOpen(false)} title={t.shell.moreSheet} placement="bottom">
        <ul className="m-0 flex list-none flex-col p-2">
          {rest.map((i) => (
            <li key={i.key}>
              <Link
                href={i.href}
                onClick={() => setMoreOpen(false)}
                aria-current={i.key === current ? "page" : undefined}
                className={cn("flex min-h-12 items-center gap-3 rounded-sm px-3 text-15 no-underline", i.key === current ? "bar-start bg-rust-50 font-bold text-rust-700" : "text-charcoal hover:bg-subtle")}
              >
                <Icon name={i.icon} size={22} />
                <span className="flex-1">{t.nav.lender[i.key]}</span>
                {badges?.[i.key] ? <span className="rounded-pill bg-ink px-2 text-12 leading-5 font-semibold text-white">{badges[i.key]}</span> : null}
              </Link>
            </li>
          ))}
          <li className="my-1 border-t border-divider" aria-hidden="true" />
          <li>
            <Link href="/help" onClick={() => setMoreOpen(false)} className="flex min-h-12 items-center gap-3 rounded-sm px-3 text-15 text-charcoal no-underline hover:bg-subtle">
              <Icon name="help" size={22} />
              {t.nav.lender.help}
            </Link>
          </li>
          <li>
            <Link href="/profile" onClick={() => setMoreOpen(false)} className="flex min-h-12 items-center gap-3 rounded-sm px-3 text-15 text-charcoal no-underline hover:bg-subtle">
              <Icon name="manage_accounts" size={22} />
              {t.shell.profile}
            </Link>
          </li>
          <li className="flex min-h-12 items-center gap-3 px-3">
            <Icon name="translate" size={22} className="text-charcoal" />
            <LocaleSwitch variant="link" className="px-0" />
          </li>
          <li>
            <button
              type="button"
              onClick={logout}
              aria-busy={busy === "logout" || undefined}
              className="flex min-h-12 w-full items-center gap-3 rounded-sm bg-transparent px-3 text-15 text-err hover:bg-err-bg"
            >
              <Icon name="logout" size={22} />
              {busy === "logout" ? t.shell.loggingOut : t.shell.logout}
            </button>
          </li>
        </ul>
        <p className="m-0 flex items-center gap-1.5 px-5 pb-5 text-12 text-muted">
          <Icon name="shield_person" size={16} />
          {t.common.privateContent} · {t.shell.secureSession}
        </p>
      </Drawer>
    </>
  );
}
