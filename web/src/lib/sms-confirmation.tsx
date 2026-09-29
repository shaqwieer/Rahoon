"use client";

import { createContext, useContext, type ReactNode } from "react";

const SmsConfirmationContext = createContext(true);

/** Provided by the portal layouts from /auth/me (Auth:SmsConfirmation on the API). */
export function SmsConfirmationProvider({ enabled, children }: { enabled: boolean; children: ReactNode }) {
  return <SmsConfirmationContext.Provider value={enabled}>{children}</SmsConfirmationContext.Provider>;
}

/**
 * false when codes are not sent by SMS and are confirmed automatically: buttons say "confirm" instead of
 * "send code". The flows themselves follow each response's `otpRequired`, so a missing provider only affects labels.
 */
export const useSmsConfirmation = () => useContext(SmsConfirmationContext);
