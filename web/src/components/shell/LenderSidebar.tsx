"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Avatar } from "@/components/ui/Avatar";
import { Icon } from "@/components/ui/Icon";
import { Logo } from "@/components/ui/Logo";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { activeKey, LENDER_NAV, visibleNav, type LenderNavKey } from "./nav";
import { OrgSwitcher } from "./OrgSwitcher";
import type { ShellUser } from "./types";

export interface LenderSidebarProps {
  user: ShellUser;
  /** Optional counts per item (tasks, approvals, complaints) — rendered as readable text badges. */
  badges?: Partial<Record<LenderNavKey, number>>;
}

/**
 * LenderSidebar.dc: 264px at ≥1280, 72px icon rail with tooltips at 768–1279, hidden below 768
 * (the mobile bottom nav takes over). Active item: rust tint + orange inline-start bar + aria-current.
 */
export function LenderSidebar({ user, badges }: LenderSidebarProps) {
  const { t } = useI18n();
  const pathname = usePathname();
  const items = visibleNav(LENDER_NAV, user.permissions);
  const current = activeKey(items, pathname);
  const helpActive = pathname === "/help" || pathname.startsWith("/help/");

  return (
    <nav
      aria-label={t.nav.lender.label}
      className="sticky top-0 z-40 hidden h-dvh w-[72px] flex-none flex-col border-e border-line bg-white md:flex xl:w-[264px]"
    >
      <div className="hidden px-5 pt-5 pb-3 xl:block">
        <Link href="/portfolio" className="inline-block rounded-xs">
          <Logo variant="horizontal" width={172} alt={t.brand.name} priority />
        </Link>
      </div>
      <div className="flex justify-center pt-4 pb-2 xl:hidden">
        <Link href="/portfolio" className="inline-block rounded-xs">
          <Logo variant="symbol" width={28} alt={t.brand.name} priority />
        </Link>
      </div>

      <div className="hidden xl:block">
        <OrgSwitcher user={user} />
      </div>
      <div className="xl:hidden">
        <OrgSwitcher user={user} collapsed />
      </div>

      <ul className="m-0 flex list-none flex-col gap-0.5 px-3 py-0">
        {items.map((item) => {
          const active = item.key === current;
          const badge = badges?.[item.key];
          const label = t.nav.lender[item.key];
          return (
            <li key={item.key} className="group relative">
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex min-h-10 items-center gap-3 rounded-sm px-3 text-14 no-underline max-xl:justify-center max-xl:px-0",
                  active ? "bar-start bg-rust-50 font-bold text-rust-700 hover:text-rust-700" : "font-medium text-charcoal hover:bg-subtle hover:text-charcoal",
                )}
              >
                <Icon name={item.icon} size={20} />
                <span className="flex-1 max-xl:sr-only">{label}</span>
                {badge ? (
                  <span className="rounded-pill bg-ink px-[7px] text-12 leading-5 font-semibold text-white max-xl:absolute max-xl:top-0 max-xl:end-1 max-xl:px-1.5 max-xl:text-11 max-xl:leading-4">
                    {badge}
                  </span>
                ) : null}
              </Link>
              <Tooltip label={label} />
            </li>
          );
        })}
      </ul>

      <div className="mt-auto flex flex-col gap-0.5 border-t border-divider p-3">
        <div className="group relative">
          <Link
            href="/help"
            aria-current={helpActive ? "page" : undefined}
            className={cn(
              "flex min-h-10 items-center gap-3 rounded-sm px-3 text-14 no-underline max-xl:justify-center max-xl:px-0",
              helpActive ? "bar-start bg-rust-50 font-bold text-rust-700" : "text-charcoal hover:bg-subtle hover:text-charcoal",
            )}
          >
            <Icon name="help" size={20} />
            <span className="max-xl:sr-only">{t.nav.lender.help}</span>
          </Link>
          <Tooltip label={t.nav.lender.help} />
        </div>
        <div className="flex items-center gap-2.5 px-3 py-2.5 max-xl:justify-center max-xl:px-0" title={user.name}>
          <Avatar initials={user.initials} size={32} />
          <span className="flex min-w-0 flex-col max-xl:sr-only">
            <strong className="truncate text-13 leading-[18px]">{user.name}</strong>
            <span className="text-12 leading-[18px] text-muted">{t.shell.secureSession}</span>
          </span>
        </div>
      </div>
    </nav>
  );
}

/** Visual tooltip for the collapsed rail (the link keeps its own sr-only name, so this is aria-hidden). */
function Tooltip({ label }: { label: string }) {
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none invisible absolute start-[calc(100%+8px)] top-1/2 z-50 -translate-y-1/2 rounded-sm bg-ink px-2.5 py-1.5 text-12 font-semibold whitespace-nowrap text-white opacity-0 shadow-2 transition-opacity duration-[var(--dur-fast)] group-focus-within:visible group-focus-within:opacity-100 group-hover:visible group-hover:opacity-100 xl:hidden"
    >
      {label}
    </span>
  );
}
