"use client";

import { Avatar } from "@/components/ui/Avatar";
import { Menu } from "@/components/ui/Menu";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { useSessionActions } from "./session";

export interface UserMenuProps {
  name: string;
  initials: string;
  /** Show the profile link (institutional users). Owners have no profile page. */
  profileHref?: string | null;
  /** Trigger look: avatar button (topbars) or name text (portal headers). */
  appearance?: "avatar" | "name" | "inverse";
  align?: "start" | "end";
  placement?: "bottom" | "top";
  className?: string;
}

/** Account menu: «حسابي والأمان» and «تسجيل الخروج» (POST /api/auth/logout, then a full reload). */
export function UserMenu({ name, initials, profileHref = "/profile", appearance = "avatar", align = "end", placement = "bottom", className }: UserMenuProps) {
  const { t } = useI18n();
  const { logout, busy } = useSessionActions();
  return (
    <Menu
      className={className}
      label={t.shell.userMenu}
      triggerLabel={appearance === "avatar" ? `${t.shell.userMenu}: ${name}` : undefined}
      align={align}
      placement={placement}
      minWidth={220}
      header={name}
      triggerClassName={cn(
        "inline-flex flex-none items-center gap-2 rounded-sm",
        appearance === "avatar" && "size-10 justify-center hover:bg-subtle",
        appearance === "name" && "min-h-10 px-2 text-13 text-muted hover:bg-subtle hover:text-ink",
        appearance === "inverse" && "min-h-10 w-full px-2 text-13 text-white hover:bg-inv-raised",
      )}
      trigger={
        appearance === "avatar" ? (
          <Avatar initials={initials} size={32} />
        ) : (
          <>
            <Avatar initials={initials} size={32} tone={appearance === "inverse" ? "ink" : "subtle"} className={appearance === "inverse" ? "border border-inv-line" : undefined} />
            <span className="truncate">{name}</span>
          </>
        )
      }
      items={[
        ...(profileHref ? [{ key: "profile", label: t.shell.profile, icon: "manage_accounts", href: profileHref }] : []),
        {
          key: "logout",
          label: busy === "logout" ? t.shell.loggingOut : t.shell.logout,
          icon: "logout",
          onSelect: logout,
          disabled: busy === "logout",
        },
      ]}
    />
  );
}
