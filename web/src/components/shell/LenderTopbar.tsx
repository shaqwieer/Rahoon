"use client";

import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { IconButton } from "@/components/ui/IconButton";
import { useI18n } from "@/lib/i18n/client";
import { LocaleSwitch } from "./LocaleSwitch";
import type { Crumb } from "./nav";
import { UserMenu } from "./UserMenu";
import type { ShellUser } from "./types";

export interface LenderTopbarProps {
  user: ShellUser;
  crumbs: Crumb[];
  onOpenSearch: () => void;
}

/** LenderTopbar.dc (64px): breadcrumb, search trigger (Ctrl K), «محتوى خاص», EN/ع, bell, account menu. */
export function LenderTopbar({ user, crumbs, onOpenSearch }: LenderTopbarProps) {
  const { t } = useI18n();
  return (
    <header className="sticky top-0 z-30 hidden h-16 items-center gap-4 border-b border-line bg-white px-8 md:flex">
      <Breadcrumb crumbs={crumbs} />
      <div role="search" className="ms-auto w-[380px] max-w-[40%]">
        <button
          type="button"
          onClick={onOpenSearch}
          aria-keyshortcuts="Control+K Meta+K"
          className="flex h-10 w-full items-center gap-2 rounded-sm border border-line-strong bg-warm px-3 text-start hover:bg-subtle"
        >
          <Icon name="search" size={20} className="text-muted" />
          <span className="flex-1 truncate text-14 text-muted">{t.shell.searchPlaceholder}</span>
          <kbd dir="ltr" className="rounded-xs border border-line bg-white px-[5px] py-px font-mono text-11 font-medium text-muted max-lg:hidden">
            {t.shell.searchShortcut}
          </kbd>
        </button>
      </div>
      <span className="inline-flex items-center gap-1.5 text-12 whitespace-nowrap text-muted max-lg:hidden">
        <Icon name="shield_person" size={16} />
        {t.common.privateContent}
      </span>
      <LocaleSwitch />
      <IconButton
        href="/notifications"
        label={t.shell.notificationsUnread(user.unread)}
        icon="notifications"
        variant="outline"
        size={40}
        badge={user.unread}
      />
      <UserMenu name={user.name} initials={user.initials} />
    </header>
  );
}

export function Breadcrumb({ crumbs, className }: { crumbs: Crumb[]; className?: string }) {
  const { t } = useI18n();
  if (!crumbs.length) return <span className={className} />;
  return (
    <nav aria-label={t.nav.breadcrumb} className={className}>
      <ol className="m-0 flex min-w-0 list-none items-center gap-1.5 p-0 text-14">
        {crumbs.map((c, i) => {
          const last = i === crumbs.length - 1;
          const text = c.ltr ? (
            <bdi dir="ltr" className="font-mono">
              {c.label}
            </bdi>
          ) : (
            c.label
          );
          return (
            <li key={i} className="flex min-w-0 items-center gap-1.5">
              {i > 0 ? <Icon name="chevron_left" size={18} mirror className="text-line-strong" /> : null}
              {last ? (
                <span aria-current="page" className="truncate font-semibold whitespace-nowrap">
                  {text}
                </span>
              ) : (
                <Link href={c.href ?? "#"} className="text-muted no-underline hover:text-ink hover:underline">
                  {text}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
