"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState, type ReactNode } from "react";
import { useSessionActions } from "@/components/shell/session";
import type { ShellUser } from "@/components/shell/types";
import { Avatar } from "@/components/ui/Avatar";
import { Drawer } from "@/components/ui/Dialog";
import { Icon } from "@/components/ui/Icon";
import { IconButton } from "@/components/ui/IconButton";
import { Logo } from "@/components/ui/Logo";
import { SkipLink } from "@/components/ui/SkipLink";
import { cn } from "@/lib/cn";
import { I18nProvider, useI18n } from "@/lib/i18n/client";

const c = { brand: "فريق رهون", navLabel: "تنقل فريق رهون", signOut: "تسجيل الخروج" };

type TeamNavKey = "overview" | "sale" | "buyers" | "opportunities" | "interests" | "messages" | "organizations" | "members" | "roles" | "audit";
/**
 * One console for the whole team. Each entry shows only to members holding one of its permissions (and, when `all`
 * is set, holding it for all of the team's work); the API checks every call again.
 */
const TEAM_NAV: Array<{ key: TeamNavKey; href: string; icon: string; anyOf: string[]; all?: boolean; label: string }> = [
  { key: "overview", href: "/team", icon: "dashboard", anyOf: ["dashboard.view"], label: "مهامي والملخص" },
  { key: "sale", href: "/team/sale", icon: "sell", anyOf: ["market.view"], label: "طلبات البيع" },
  { key: "buyers", href: "/team/buyers", icon: "person_search", anyOf: ["market.view"], label: "طلبات المشترين" },
  { key: "opportunities", href: "/team/opportunities", icon: "home_work", anyOf: ["market.view"], label: "الفرص" },
  { key: "interests", href: "/team/interests", icon: "handshake", anyOf: ["market.view"], label: "الاهتمامات" },
  { key: "messages", href: "/team/messages", icon: "mail", anyOf: ["market.follow"], all: true, label: "رسائل التواصل" },
  { key: "organizations", href: "/team/organizations", icon: "domain", anyOf: ["directory.read", "directory.manage"], label: "دليل الجهات" },
  { key: "members", href: "/team/members", icon: "group", anyOf: ["team.read"], label: "الفريق" },
  { key: "roles", href: "/team/roles", icon: "admin_panel_settings", anyOf: ["roles.read"], label: "الأدوار والصلاحيات" },
  { key: "audit", href: "/team/audit", icon: "history", anyOf: ["audit.read"], label: "السجل" },
];

function useNav(user: ShellUser) {
  const pathname = usePathname();
  const items = TEAM_NAV.filter((i) => i.anyOf.some((p) => user.permissions.includes(p) && (!i.all || user.scopes[p] === "all")));
  const current =
    items
      .filter((i) => pathname === i.href || pathname.startsWith(`${i.href}/`))
      .sort((a, b) => b.href.length - a.href.length)[0]?.key ?? null;
  return { items, current };
}

/**
 * «فريق رهون» workspace (design request D-4 shell): 264px sidebar ≥1280, icon rail 768–1279, top bar + drawer below
 * 768. Its own navigation; no command palette or bell.
 */
export function TeamShell({ user, children }: { user: ShellUser; children: ReactNode }) {
  const { locale } = useI18n();
  return (
    <I18nProvider locale={locale} numerals={user.numerals}>
      <TeamShellInner user={user}>{children}</TeamShellInner>
    </I18nProvider>
  );
}

function TeamShellInner({ user, children }: { user: ShellUser; children: ReactNode }) {
  const { t } = useI18n();
  const { items, current } = useNav(user);
  const [open, setOpen] = useState(false);
  const { logout, busy } = useSessionActions();

  const links = (onNavigate?: () => void, rail = false) => (
    <ul className="m-0 flex list-none flex-col gap-0.5 px-3 py-0">
      {items.map((i) => {
        const on = i.key === current;
        return (
          <li key={i.key}>
            <Link
              href={i.href}
              onClick={onNavigate}
              aria-current={on ? "page" : undefined}
              title={rail ? i.label : undefined}
              className={cn(
                "flex min-h-10 items-center gap-3 rounded-sm px-3 text-14 no-underline",
                rail && "max-xl:justify-center max-xl:px-0",
                on ? "bar-start bg-rust-50 font-bold text-rust-700 hover:text-rust-700" : "font-medium text-charcoal hover:bg-subtle hover:text-charcoal",
              )}
            >
              <Icon name={i.icon} size={20} />
              <span className={cn("flex-1", rail && "max-xl:sr-only")}>{i.label}</span>
            </Link>
          </li>
        );
      })}
    </ul>
  );

  return (
    <div className="flex min-h-dvh">
      <SkipLink />
      <nav aria-label={c.navLabel} className="sticky top-0 z-40 hidden h-dvh w-[72px] flex-none flex-col border-e border-line bg-white md:flex xl:w-[264px]">
        <div className="hidden px-5 pt-5 pb-3 xl:block">
          <Link href="/team" className="inline-block rounded-xs">
            <Logo variant="horizontal" width={172} alt={t.brand.name} priority />
          </Link>
        </div>
        <div className="flex justify-center pt-4 pb-2 xl:hidden">
          <Link href="/team" className="inline-block rounded-xs">
            <Logo variant="symbol" width={28} alt={t.brand.name} priority />
          </Link>
        </div>
        <div className="mx-3 mt-1 mb-3 hidden flex-col gap-0.5 rounded-[8px] bg-subtle px-3 py-2 xl:flex">
          <strong className="text-14">{c.brand}</strong>
          <span className="text-12 text-muted">{user.roleName}</span>
        </div>
        {links(undefined, true)}
        <div className="mt-auto flex flex-col gap-1 border-t border-divider p-3">
          <div className="flex items-center gap-2.5 px-3 py-2 max-xl:justify-center max-xl:px-0" title={user.name}>
            <Avatar initials={user.initials} size={32} />
            <span className="flex min-w-0 flex-col max-xl:sr-only">
              <strong className="truncate text-13 leading-[18px]">{user.name}</strong>
              <span className="text-12 leading-[18px] text-muted">{user.roleName}</span>
            </span>
          </div>
          <div className="flex items-center gap-1 max-xl:flex-col">
            <button
              type="button"
              onClick={logout}
              aria-busy={busy === "logout" || undefined}
              className="flex min-h-10 flex-1 items-center gap-2 rounded-sm bg-transparent px-3 text-13 text-err hover:bg-err-bg max-xl:justify-center max-xl:px-0"
            >
              <Icon name="logout" size={18} />
              <span className="max-xl:sr-only">{c.signOut}</span>
            </button>
          </div>
        </div>
      </nav>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2.5 border-b border-line bg-white ps-2 pe-4 md:hidden">
          <IconButton label={c.navLabel} icon="menu" size={44} iconSize={22} onClick={() => setOpen(true)} />
          <Logo variant="symbol" width={26} alt={t.brand.name} priority />
          <span className="min-w-0 flex-1 truncate text-14 font-semibold">
            {c.brand} <span className="font-normal text-muted">· {user.roleName}</span>
          </span>
        </header>
        <main id="main" tabIndex={-1} className="flex-1 px-4 pt-4 pb-10 outline-none md:px-8 md:pt-7 xl:px-10">
          {children}
        </main>
      </div>

      <Drawer open={open} onClose={() => setOpen(false)} title={c.brand}>
        <div className="flex flex-col gap-3 py-2">
          {links(() => setOpen(false))}
          <div className="flex items-center gap-2 border-t border-divider px-3 pt-3">
            <button type="button" onClick={logout} className="flex min-h-11 flex-1 items-center gap-2 rounded-sm bg-transparent px-3 text-14 text-err hover:bg-err-bg">
              <Icon name="logout" size={20} />
              {c.signOut}
            </button>
          </div>
        </div>
      </Drawer>
    </div>
  );
}
