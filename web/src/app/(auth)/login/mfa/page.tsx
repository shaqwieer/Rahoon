import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthSplit } from "@/components/shell/Public";
import { getMe } from "@/lib/api/server";
import { safeNext } from "@/lib/api/types";
import { getServerDictionary } from "@/lib/i18n/server";
import { MfaForm } from "./MfaForm";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.mfa.title };
}

/** S04 — second factor after the password step (desktop reuses the S03 split layout). */
export default async function MfaPage({ searchParams }: PageProps<"/login/mfa">) {
  const sp = await searchParams;
  const next = safeNext(typeof sp.next === "string" ? sp.next : null, "");
  const { t } = await getServerDictionary();

  const me = await getMe().catch(() => null);
  if (me && !me.authenticated) redirect(next ? `/login?next=${encodeURIComponent(next)}` : "/login");
  if (me?.authenticated && me.stage === "active") redirect(me.scope === "none" ? "/select-context" : next || me.home);

  return (
    <AuthSplit quote={t.auth.login.asideQuote} note={t.auth.login.asideNote}>
      <MfaForm next={next} />
    </AuthSplit>
  );
}
