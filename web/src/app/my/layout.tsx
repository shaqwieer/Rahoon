import { requireIndividualPortal } from "@/lib/api/guards";
import { SmsConfirmationProvider } from "@/lib/sms-confirmation";

/** Individual area (ADR 0001): self-registered individuals only; the API authorizes every call. */
export default async function MyLayout({ children }: LayoutProps<"/my">) {
  const me = await requireIndividualPortal();
  return <SmsConfirmationProvider enabled={me.smsConfirmation !== false}>{children}</SmsConfirmationProvider>;
}
