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
