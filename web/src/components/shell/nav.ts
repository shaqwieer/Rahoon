import type { Dictionary } from "@/lib/i18n";

/** Lender workspace navigation (LenderSidebar). Items are hidden when the member lacks the permission. */
export type LenderNavKey = "portfolio" | "cases" | "tasks" | "approvals" | "complaints" | "reports" | "settings";

export interface NavItemDef<K extends string = string> {
  key: K;
  href: string;
  icon: string;
  /** Permission required to see the item; `null` = always visible. */
  permission: string | null;
}

export const LENDER_NAV: NavItemDef<LenderNavKey>[] = [
  { key: "portfolio", href: "/portfolio", icon: "dashboard", permission: "portfolio.view" },
  { key: "cases", href: "/cases", icon: "folder_open", permission: "case.view" },
  { key: "tasks", href: "/tasks", icon: "task_alt", permission: null },
  { key: "approvals", href: "/approvals", icon: "approval", permission: "solution.approve" },
  { key: "complaints", href: "/complaints", icon: "support_agent", permission: "complaint.view" },
  { key: "reports", href: "/reports", icon: "bar_chart", permission: "reports.view" },
  { key: "settings", href: "/settings", icon: "settings", permission: "org.settings" },
];

export function visibleNav<K extends string>(items: NavItemDef<K>[], permissions: readonly string[]): NavItemDef<K>[] {
  return items.filter((i) => i.permission === null || permissions.includes(i.permission));
}

export function isActivePath(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}

export type SettingsNavKey = "org" | "users" | "docs" | "limits" | "templates" | "sla" | "providers" | "reports";
export const SETTINGS_NAV: Array<{ key: SettingsNavKey; href: string }> = [
  { key: "org", href: "/settings" },
  { key: "users", href: "/settings/users" },
  { key: "docs", href: "/settings/documents" },
  { key: "limits", href: "/settings/limits" },
  { key: "templates", href: "/settings/templates" },
  { key: "sla", href: "/settings/sla" },
  { key: "providers", href: "/settings/providers" },
  { key: "reports", href: "/settings/reports" },
];

export type PlatformNavKey =
  | "ops"
  | "inst"
  | "users"
  | "cases"
  | "defaults"
  | "providers"
  | "complaints"
  | "audit"
  | "privacy"
  | "workflow"
  | "billing"
  | "integrations";

export const PLATFORM_NAV: NavItemDef<PlatformNavKey>[] = [
  { key: "ops", href: "/platform", icon: "monitoring", permission: "platform.ops" },
  { key: "inst", href: "/platform/institutions", icon: "domain", permission: "platform.institutions" },
  { key: "users", href: "/platform/users", icon: "manage_accounts", permission: "platform.users" },
  { key: "cases", href: "/platform/cases", icon: "visibility_lock", permission: "platform.temp_access" },
  { key: "defaults", href: "/platform/defaults", icon: "tune", permission: "platform.defaults" },
  { key: "providers", href: "/platform/providers", icon: "assignment_ind", permission: "platform.institutions" },
  { key: "complaints", href: "/platform/complaints", icon: "support_agent", permission: "platform.complaints" },
  { key: "audit", href: "/platform/audit", icon: "history", permission: "platform.audit" },
  { key: "privacy", href: "/platform/privacy", icon: "policy", permission: "platform.privacy" },
  { key: "workflow", href: "/platform/workflow", icon: "account_tree", permission: "platform.defaults" },
  { key: "billing", href: "/platform/billing", icon: "receipt_long", permission: "platform.billing" },
  { key: "integrations", href: "/platform/integrations", icon: "hub", permission: "platform.integrations" },
];

export type OwnerNavKey = "home" | "docs" | "options" | "payments" | "help";
export const OWNER_NAV: Array<{ key: OwnerNavKey; href: string; icon: string }> = [
  { key: "home", href: "/owner", icon: "space_dashboard" },
  { key: "docs", href: "/owner/documents", icon: "folder" },
  { key: "options", href: "/owner/options", icon: "tips_and_updates" },
  { key: "payments", href: "/owner/payments", icon: "payments" },
  { key: "help", href: "/owner/help", icon: "support" },
];

/** Active item for a pathname: the longest matching href wins (so /platform doesn't swallow /platform/audit). */
export function activeKey<K extends string>(items: Array<{ key: K; href: string }>, pathname: string): K | null {
  let best: { key: K; len: number } | null = null;
  for (const i of items) {
    if (isActivePath(pathname, i.href) && (!best || i.href.length > best.len)) best = { key: i.key, len: i.href.length };
  }
  return best?.key ?? null;
}

export interface Crumb {
  label: string;
  href?: string;
  /** LTR value (e.g. a case reference) rendered in <bdi dir="ltr"> with mono font. */
  ltr?: boolean;
}

/** Topbar breadcrumb derived from the URL: section › sub-page (case refs are shown LTR). */
export function lenderCrumbs(pathname: string, t: Dictionary): Crumb[] {
  const seg = pathname.split("/").filter(Boolean);
  const section = seg[0];
  const navLabels: Record<string, string> = {
    portfolio: t.nav.lender.portfolio,
    cases: t.nav.lender.cases,
    tasks: t.nav.lender.tasks,
    approvals: t.nav.lender.approvals,
    complaints: t.nav.lender.complaints,
    reports: t.nav.lender.reports,
    analytics: t.nav.lender.reports,
    settings: t.nav.lender.settings,
    notifications: t.shell.notifications,
    search: t.common.search,
    profile: t.shell.profile,
    help: t.nav.lender.help,
  };
  if (!section) return [];
  const first: Crumb = { label: navLabels[section] ?? section, href: `/${section}` };
  if (seg.length === 1) return [{ label: first.label }];
  if (section === "settings") {
    const sub = SETTINGS_NAV.find((s) => s.href === `/settings/${seg[1]}`);
    return [first, { label: sub ? t.nav.settings[sub.key] : decodeURIComponent(seg[1]) }];
  }
  if (section === "cases") return [first, { label: decodeURIComponent(seg[1]), ltr: true }];
  return [first, { label: decodeURIComponent(seg[seg.length - 1]) }];
}
