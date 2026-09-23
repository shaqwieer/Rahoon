import type { MeAuthenticated, MeMembership } from "@/lib/api/types";
import type { Numerals } from "@/lib/format";

/** Serializable slice of /api/auth/me passed from a server layout to a client shell. */
export interface ShellUser {
  name: string;
  initials: string;
  orgName: string;
  orgInitials: string;
  /** Role label in Arabic from the API (e.g. «مديرة حالات»). */
  roleName: string;
  permissions: string[];
  memberships: MeMembership[];
  unread: number;
  numerals: Numerals;
}

export function toShellUser(me: MeAuthenticated): ShellUser {
  return {
    name: me.user.name,
    initials: me.user.initials,
    orgName: me.organization?.name ?? me.owner?.lenderName ?? "",
    orgInitials: me.organization?.initials ?? "",
    roleName: me.roleName ?? "",
    permissions: me.permissions,
    memberships: me.memberships,
    unread: me.unreadNotifications,
    numerals: me.user.numerals === "arab" ? "arab" : "latn",
  };
}
