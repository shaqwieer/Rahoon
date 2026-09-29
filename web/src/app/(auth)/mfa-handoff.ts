/** Per-tab hand-off between the login form and the MFA step (never put codes or destinations in the URL). */
export const MFA_STORAGE_KEY = "rahoon_mfa";

export interface MfaHandoff {
  /** Masked phone, e.g. «+966 5• ••• ••81». */
  destination: string;
  /** Epoch ms after which «إعادة إرسال الرمز» is enabled. */
  resendAt: number;
  /** Development-only simulated SMS code. */
  sandboxCode: string | null;
}

/** Where to go after a successful second factor: keep the deep link unless the user must pick a workspace. */
export function destinationAfterMfa(serverNext: string, next: string) {
  if (serverNext === "/select-context") return next ? `/select-context?next=${encodeURIComponent(next)}` : serverNext;
  if (serverNext === "/access-denied" || !next) return serverNext;
  return next;
}
