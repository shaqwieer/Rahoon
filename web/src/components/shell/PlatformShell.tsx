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
import { LocaleSwitch } from "./LocaleSwitch";
import { activeKey, PLATFORM_NAV, visibleNav } from "./nav";
import type { ShellUser } from "./types";
import { UserMenu } from "./UserMenu";

/** PlatformSidebar.dc: dark 264px rail — logo (dark artwork), «إدارة المنصة» + role, 12 items, footer notice. */
export function PlatformSidebar({ user, onNavigate, className }: { user: ShellUser; onNavigate?: () => void; className?: string }) {
  const { t } = useI18n();
  const pathname = usePathname();
  const items = visibleNav(PLATFORM_NAV, user.permissions);
  const current = activeKey(items, pathname);
  return (
    <nav aria-label={t.nav.platform.label} className={cn("surface-dark flex w-[264px] flex-none flex-col bg-inv text-white", className)}>
      <div className="px-5 pt-5 pb-2">
        <Link href="/platform" onClick={onNavigate} className="inline-block rounded-xs">
          <Logo variant="horizontal-dark" width={172} alt={t.brand.name} priority />
        </Link>
      </div>
      <div className="mx-3 mt-1.5 mb-3 flex flex-col gap-0.5 rounded-[8px] bg-inv-raised px-2.5 py-2">
        <strong className="text-13">{t.nav.platform.title}</strong>
        <span className="text-12 text-inv-2">
          {user.roleName} · {t.nav.platform.restricted}
        </span>
      </div>
      <ul className="m-0 flex list-none flex-col gap-0.5 px-3 py-0">
        {items.map((i) => {
          const on = i.key === current;
          return (
            <li key={i.key}>
              <Link
                href={i.href}
                onClick={onNavigate}
                aria-current={on ? "page" : undefined}
                className={cn(
                  "flex min-h-[38px] items-center gap-3 rounded-sm px-3 text-14 text-white no-underline hover:bg-inv-raised hover:text-white",
                  on ? "bar-start bg-inv-raised font-bold" : "font-medium",
                )}
              >
                <Icon name={i.icon} size={20} />
                {t.nav.platform[i.key]}
              </Link>
            </li>
          );
        })}
      </ul>
      <div className="mt-auto flex flex-col gap-2 border-t border-inv-line px-3 pt-3">
        <div className="flex items-center gap-1">
          <UserMenu name={user.name} initials={user.initials} appearance="inverse" align="start" placement="top" className="min-w-0 flex-1" />
          <LocaleSwitch variant="inverse" className="px-2 text-13" />
        </div>
      </div>
      <div className="flex items-center gap-1.5 px-5 py-3.5 text-12 text-inv-2">
        <Icon name="shield" size={16} />
        {t.nav.platform.footer}
      </div>
    </nav>
  );
}

/** Platform administration shell: sticky dark sidebar ≥1024; below that a dark top bar opens it as a drawer. No topbar on desktop (B7 §1.3). */
export function PlatformShell({ user, children }: { user: ShellUser; children: ReactNode }) {
  const { t, locale } = useI18n();
  const [open, setOpen] = useState(false);
  return (
    <I18nProvider locale={locale} numerals={user.numerals}>
      <div className="flex min-h-dvh">
        <SkipLink />
        <PlatformSidebar user={user} className="sticky top-0 hidden h-dvh overflow-visible lg:flex" />
        <div className="flex min-w-0 flex-1 flex-col">
          <header className="surface-dark sticky top-0 z-30 flex h-14 items-center gap-2.5 bg-inv ps-2 pe-4 text-white lg:hidden">
            <IconButton label={t.nav.platform.label} icon="menu" variant="inverse" size={44} onClick={() => setOpen(true)} aria-haspopup="dialog" aria-expanded={open} />
            <Logo variant="horizontal-dark" width={170} alt={t.brand.name} />
            <span className="ms-auto truncate text-12 text-inv-2">{user.roleName}</span>
          </header>
          <main id="main" tabIndex={-1} className="flex flex-1 flex-col gap-[18px] px-4 py-5 outline-none md:px-10 md:py-8">
            {children}
          </main>
        </div>
        <Drawer open={open} onClose={() => setOpen(false)} title={t.nav.platform.title} hideTitle placement="end" className="!bg-inv">
          <PlatformSidebar user={user} onNavigate={() => setOpen(false)} className="min-h-full w-full" />
        </Drawer>
      </div>
    </I18nProvider>
  );
}
