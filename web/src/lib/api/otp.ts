/** What every endpoint that issues a one-time code returns. */
export interface OtpIssued {
  destination: string;
  resendInSeconds?: number;
  /** Echoed in development (sandbox SMS), and always when SMS confirmation is off. */
  sandboxCode?: string | null;
  /** false when the API runs with Auth:SmsConfirmation=false: nothing was sent and the code is confirmed automatically. */
  otpRequired?: boolean;
}

/** The code to confirm without showing a code step, or null when the person must type the code they received. */
export const autoOtpCode = (issued: OtpIssued): string | null => (issued.otpRequired === false && issued.sandboxCode ? issued.sandboxCode : null);
