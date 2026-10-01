/** Shapes of /api/team/admin (server/src/Rahoon.Api/Modules/Identity/TeamAdminEndpoints.cs). */

export type Scope = "assigned" | "all";
export type MemberStatus = "active" | "suspended" | "revoked" | "invited";

export interface CatalogPermission { key: string; nameAr: string; nameEn: string; scopable: boolean; reserved: boolean; reservedFor: string | null }
export interface CatalogArea { key: string; nameAr: string; permissions: CatalogPermission[] }
export interface Catalog { areas: CatalogArea[]; scopes: { key: Scope; nameAr: string }[] }

export interface RoleRow {
  id: string;
  key: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  isSystem: boolean;
  archived: boolean;
  members: number;
  version: number;
  grants: { key: string; scope: Scope }[];
}

export interface MemberRow {
  id: string;
  userId: string;
  name: string;
  email: string;
  title: string | null;
  status: MemberStatus;
  roles: { id: string; nameAr: string; isSystem: boolean }[];
  lastLoginAt: string | null;
  openWork: number;
  self: boolean;
}

export interface MemberDetail {
  id: string;
  userId: string;
  name: string;
  email: string;
  phoneMasked: string | null;
  title: string | null;
  status: MemberStatus;
  statusReason: string | null;
  statusChangedAt: string | null;
  createdAt: string;
  lastLoginAt: string | null;
  activeSessions: number;
  roles: { id: string; key: string; nameAr: string; isSystem: boolean; archived: boolean }[];
  effective: { key: string; nameAr: string; area: string; scope: Scope; scopable: boolean; from: { roleId: string; roleName: string; scope: Scope }[] }[];
  workload: { saleRequests: number; buyerRequests: number; opportunities: number; interests: number; total: number };
  history: { seq: number; type: string; title: string; actorLabel: string | null; reason: string | null; fromState: string | null; toState: string | null; occurredAt: string; blocked: boolean }[];
  isOwner: boolean;
  lastOwner: boolean;
  self: boolean;
  actions: { manage: boolean; blocked: string | null; changeRoles: boolean; suspend: boolean; reactivate: boolean; remove: boolean; reassign: boolean };
  assignableRoles: { id: string; nameAr: string; isSystem: boolean }[];
}

export interface InvitationRow {
  id: string;
  email: string;
  fullName: string;
  phoneMasked: string;
  title: string | null;
  roles: string[];
  status: "pending" | "expired" | "accepted" | "revoked";
  invitedByLabel: string;
  createdAt: string;
  expiresAt: string;
  acceptedAt: string | null;
  revokedAt: string | null;
}

export interface InvitationIssued { id: string; link: string; expiresAt: string; delivered: false; delivery: string }

export const SCOPE_LABEL: Record<Scope, string> = { assigned: "المسند إليه فقط", all: "كل أعمال الفريق" };

export const MEMBER_STATUS: Record<MemberStatus, { label: string; tone: "ok" | "warn" | "neutral" | "info" }> = {
  active: { label: "نشط", tone: "ok" },
  suspended: { label: "موقوف", tone: "warn" },
  revoked: { label: "أُزيل", tone: "neutral" },
  invited: { label: "مدعو", tone: "info" },
};

export const INVITATION_STATUS: Record<InvitationRow["status"], { label: string; tone: "ok" | "warn" | "neutral" | "info" }> = {
  pending: { label: "بانتظار القبول", tone: "info" },
  expired: { label: "منتهية", tone: "warn" },
  accepted: { label: "مقبولة", tone: "ok" },
  revoked: { label: "ملغاة", tone: "neutral" },
};

/** The full join link for the current site (the API returns a relative `/join#token`). */
export function absoluteLink(link: string): string {
  if (typeof window === "undefined") return link;
  return `${window.location.origin}${link}`;
}
