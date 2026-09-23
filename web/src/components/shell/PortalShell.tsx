"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { cn } from "@/lib/cn";
import { I18nProvider, useI18n } from "@/lib/i18n/client";
import { LocaleSwitch } from "./LocaleSwitch";
import { activeKey } from "./nav";
import type { ShellUser } from "./types";
import { UserMenu } from "./UserMenu";

interface PortalNavItem {
  key: string;
  label: string;
  href: string;
}

interface PortalShellProps {
  user: ShellUser;
  /** «مكتب تقييم معتمد «ب» · مقيّم». */
  orgLabel: string;
  /** Mobile header title (e.g. «التكليفات»). */
  mobileTitle: string;
  nav: PortalNavItem[];
  /** Trailing note (agent: «وصول بتكليف من الجهة المختصة»). */
  note?: string;
  home: string;
  children: ReactNode;
}

/** Inline header shell shared by the service-provider and judicial-agent portals (B7 §1.1, B10 J05). No sidebar. */
function PortalShell({ user, orgLabel, mobileTitle, nav, note, home, children }: PortalShellProps) {
  const { t, locale } = useI18n();
  const pathname = usePathname();
  const current = activeKey(nav, pathname);
  return (
    <I18nProvider locale={locale} numerals={user.numerals}>
      <div className="flex min-h-dvh flex-col">
        <SkipLink />
        {/* ≥768 header (64px) */}
        <header className="sticky top-0 z-30 hidden h-16 items-center gap-6 border-b border-line bg-white px-8 md:flex">
          <Link href={home} className="inline-flex rounded-xs">
            <Logo variant="horizontal" width={172} alt={t.brand.name} priority />
          </Link>
          <span className="truncate rounded-sm bg-subtle px-2.5 py-1.5 text-14 font-semibold max-lg:hidden">{orgLabel}</span>
          <nav aria-label={t.nav.provider.label}>
            <ul className="m-0 flex list-none gap-1 p-0 text-14">
              {nav.map((n) => {
                const on = n.key === current;
                return (
                  <li key={n.key}>
                    <Link
                      href={n.href}
                      aria-current={on ? "page" : undefined}
                      className={cn("inline-flex min-h-10 items-center rounded-sm px-3 no-underline", on ? "bg-rust-50 font-bold text-rust-700 hover:text-rust-700" : "text-ink hover:bg-subtle hover:text-ink")}
                    >
                      {n.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </nav>
          <div className="ms-auto flex items-center gap-2">
            {note ? <span className="text-13 text-muted max-xl:hidden">{note}</span> : null}
            <LocaleSwitch />
            <UserMenu name={user.name} initials={user.initials} appearance="name" />
          </div>
        </header>
        {/* <768 header (60px) */}
        <header className="sticky top-0 z-30 flex h-[60px] items-center gap-2.5 border-b border-divider bg-white ps-4 pe-2 md:hidden">
          <Logo variant="symbol" width={28} alt={t.brand.name} />
          <div className="flex min-w-0 flex-1 flex-col">
            <strong className="truncate text-16">{mobileTitle}</strong>
            <span className="truncate text-12 text-muted">{user.orgName}</span>
          </div>
          <UserMenu name={user.name} initials={user.initials} />
        </header>
        <nav aria-label={t.nav.provider.label} className="flex gap-1 overflow-x-auto border-b border-line bg-white px-3 md:hidden">
          {nav.map((n) => (
            <Link
              key={n.key}
              href={n.href}
              aria-current={n.key === current ? "page" : undefined}
              className={cn("inline-flex min-h-11 items-center px-3 text-14 whitespace-nowrap no-underline", n.key === current ? "bar-bottom font-bold text-ink" : "text-muted")}
            >
              {n.label}
            </Link>
          ))}
        </nav>
        <main id="main" tabIndex={-1} className="flex-1 px-4 py-4 outline-none md:px-8 md:py-7">
          {children}
        </main>
      </div>
    </I18nProvider>
  );
}

/** Service provider / valuer portal: «التكليفات» + «المساعدة»; scope = own assignments for their duration. */
export function ProviderShell({ user, children }: { user: ShellUser; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <PortalShell
      user={user}
      home="/provider"
      orgLabel={`${user.orgName} · ${user.roleName || t.nav.provider.roleSuffix}`}
      mobileTitle={t.nav.provider.assignments}
      nav={[
        { key: "assignments", label: t.nav.provider.assignments, href: "/provider" },
        { key: "help", label: t.nav.provider.help, href: "/provider/help" },
      ]}
    >
      {children}
    </PortalShell>
  );
}

/** Judicial sale agent portal (B10 J05–J07): «الحالات المكلفة» + «المساعدة»; title «وكيل البيع القضائي». */
export function AgentShell({ user, children }: { user: ShellUser; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <PortalShell
      user={user}
      home="/agent"
      orgLabel={`${user.orgName} · ${t.nav.agent.roleSuffix}`}
      mobileTitle={t.nav.agent.title}
      note={t.nav.agent.accessNote}
      nav={[
        { key: "cases", label: t.nav.agent.cases, href: "/agent" },
        { key: "help", label: t.nav.agent.help, href: "/agent/help" },
      ]}
    >
      {children}
    </PortalShell>
  );
}
