"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { activeKey, SETTINGS_NAV } from "./nav";

/** SettingsNav.dc (220px) — organization settings sections; horizontal scroll strip below 1024px. */
export function SettingsNav({ orgName }: { orgName: string }) {
  const { t } = useI18n();
  const pathname = usePathname();
  // `/settings` is the org page; only exact match makes it active (longest-prefix for the rest).
  const current = pathname === "/settings" ? "org" : activeKey(SETTINGS_NAV.filter((s) => s.key !== "org"), pathname);
  return (
    <nav aria-label={t.nav.settings.label} className="flex flex-none flex-col gap-0.5 lg:w-[220px]">
      <span className="px-3 pt-1 pb-2 text-12 font-bold text-muted">{t.nav.settings.heading(orgName)}</span>
      <ul className="m-0 flex list-none gap-0.5 overflow-x-auto p-0 lg:flex-col">
        {SETTINGS_NAV.map((s) => {
          const active = s.key === current;
          return (
            <li key={s.key} className="flex-none">
              <Link
                href={s.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex min-h-10 items-center rounded-sm px-3 text-14 whitespace-nowrap no-underline",
                  active ? "bg-rust-50 font-bold text-rust-700 hover:text-rust-700" : "font-medium text-charcoal hover:bg-subtle hover:text-charcoal",
                )}
              >
                {t.nav.settings[s.key]}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

/** [SettingsNav 220][content] with a 28px gap (B7 §1.2); stacks below 1024px. */
export function SettingsLayout({ orgName, children }: { orgName: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:gap-7">
      <SettingsNav orgName={orgName} />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
