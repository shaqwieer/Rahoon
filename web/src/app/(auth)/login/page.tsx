import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthSplit } from "@/components/shell/Public";
import { getMe } from "@/lib/api/server";
import { safeNext } from "@/lib/api/types";
import { getServerDictionary } from "@/lib/i18n/server";
import { LoginForm } from "./LoginForm";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.login.title, description: t.auth.login.sub, robots: { index: true, follow: true } };
}

/** S03 — institutional sign-in (owners use their invitation link instead). */
export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const sp = await searchParams;
  const next = safeNext(typeof sp.next === "string" ? sp.next : null, "");
  const { t } = await getServerDictionary();

  // Already fully signed in → go straight to the workspace (API failures fall through to the form).
  const me = await getMe().catch(() => null);
  if (me?.authenticated && me.stage === "active" && me.scope !== "none") redirect(next || me.home);

  return (
    <AuthSplit quote={t.auth.login.asideQuote} note={t.auth.login.asideNote}>
      <LoginForm next={next} />
    </AuthSplit>
  );
}
