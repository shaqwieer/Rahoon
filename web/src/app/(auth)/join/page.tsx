import type { Metadata } from "next";
import { AuthSplit } from "@/components/shell/Public";
import { getServerDictionary } from "@/lib/i18n/server";
import { JoinForm } from "./JoinForm";

export const metadata: Metadata = { title: "الانضمام إلى فريق رهون", robots: { index: false, follow: false } };

/**
 * Accepting a team invitation. The one-time token is in the URL fragment (`/join#…`), which browsers never send to a
 * server, so it stays out of access logs; the form posts it to the API.
 */
export default async function JoinPage() {
  const { t } = await getServerDictionary();
  return (
    <AuthSplit quote={t.auth.login.asideQuote} note={t.auth.login.asideNote}>
      <JoinForm />
    </AuthSplit>
  );
}
