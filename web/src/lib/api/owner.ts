import "server-only";
import { apiFetch } from "./server";

/** GET /api/public/invitations/{token} (anonymous). */
export interface OwnerInvitation {
  status: "active" | "used" | "expired" | "invalid";
  lenderName?: string;
  invitationCode?: string;
  phoneMasked?: string;
  nationalIdProvider?: "enabled" | "simulated" | "pending" | "unavailable" | "failed";
}

/** Returns the invitation, or `{ status: "error" }` when the API cannot be reached (never throws). */
export async function getOwnerInvitation(token: string): Promise<OwnerInvitation | { status: "error" }> {
  try {
    const res = await apiFetch(`/public/invitations/${encodeURIComponent(token)}`);
    if (res.status === 404) return { status: "invalid" };
    if (!res.ok) return { status: "error" };
    return (await res.json()) as OwnerInvitation;
  } catch {
    return { status: "error" };
  }
}
