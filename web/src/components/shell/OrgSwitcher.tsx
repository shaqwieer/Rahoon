"use client";

import { Avatar } from "@/components/ui/Avatar";
import { Icon } from "@/components/ui/Icon";
import { Menu } from "@/components/ui/Menu";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { useSessionActions } from "./session";
import type { ShellUser } from "./types";

/**
 * Sidebar organization / role switcher (C12). Choosing another membership POSTs /api/auth/context and
 * hard-navigates to the returned `next` so nothing cached crosses tenants.
 */
export function OrgSwitcher({ user, collapsed }: { user: ShellUser; collapsed?: boolean }) {
  const { t } = useI18n();
  const { switchContext, busy } = useSessionActions();
  const items = user.memberships.map((m) => ({
    key: m.id,
    label: m.organization,
    description: m.role ?? undefined,
    checked: m.current,
    leading: <Avatar initials={m.initials} size={32} shape="tile" tone={m.current ? "ink" : "subtle"} />,
    onSelect: m.current ? undefined : () => switchContext(m.id),
    disabled: busy === "switch",
  }));
  return (
    <Menu
      label={t.shell.orgSwitcher}
      header={t.shell.orgMenuTitle}
      footer={
        <span className="flex gap-1.5">
          <Icon name="shield" size={16} />
          {busy === "switch" ? t.shell.switching : t.shell.orgMenuNote}
        </span>
      }
      minWidth={300}
      className={collapsed ? "mx-auto my-1" : "mx-3 mt-1 mb-3"}
      triggerLabel={collapsed ? `${t.shell.orgSwitcher}: ${user.orgName} · ${user.roleName}` : undefined}
      triggerClassName={cn(
        "flex items-center rounded-md border border-line bg-warm text-start hover:bg-subtle",
        collapsed ? "size-12 justify-center" : "min-h-[52px] w-full gap-2.5 px-2.5 py-2",
      )}
      trigger={
        <>
          <Avatar initials={user.orgInitials} size={32} shape="tile" tone="ink" className="text-13" />
          {collapsed ? null : (
            <>
              <span className="flex min-w-0 flex-1 flex-col">
                <strong className="truncate text-14 leading-5">{user.orgName}</strong>
                <span className="truncate text-12 leading-[18px] text-muted">{user.roleName}</span>
              </span>
              <Icon name="unfold_more" size={20} className="text-muted" />
            </>
          )}
        </>
      }
      items={items}
    />
  );
}
