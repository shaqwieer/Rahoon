import type { Metadata } from "next";
import { LenderShell } from "@/components/shell/LenderShell";
import { PublicHeader } from "@/components/shell/Public";
import { toShellUser } from "@/components/shell/types";
import { getMe } from "@/lib/api/server";
import { formatDateTime } from "@/lib/format";
import { getServerDictionary } from "@/lib/i18n/server";
import { AccessDeniedBody } from "./AccessDeniedBody";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.accessDenied.title };
}

/**
 * S12 — the same wording for "doesn't exist" and "not yours" so nothing leaks across organizations.
 * Never calls apiGet (which redirects here on 403) to avoid loops.
 */
export default async function AccessDeniedPage({ searchParams }: PageProps<"/access-denied">) {
  const sp = await searchParams;
  const { t } = await getServerDictionary();
  const A = t.auth.accessDenied;
  const me = await getMe().catch(() => null);
  const at = formatDateTime(new Date());

  if (sp.reason === "owner") {
    return (
      <>
        <PublicHeader variant="logo" />
        <main id="main" tabIndex={-1} className="mx-auto w-full max-w-[560px] px-6 py-12 outline-none">
          <AccessDeniedBody title={A.ownerTitle} body={A.ownerBody} primary={{ label: A.login, href: "/login" }} code="403-OWNER" at={at} tone="warn" icon="link_off" />
        </main>
      </>
    );
  }

  const signedIn = me?.authenticated && me.stage === "active" ? me : null;
  const body = signedIn?.organization ? (
    <>
      {A.bodyLead} {A.bodyIn} <strong>{signedIn.organization.name}</strong>
      {signedIn.roleName ? (
        <>
          {" "}
          {A.bodyRole} <strong>{signedIn.roleName}</strong>
        </>
      ) : null}
      . {A.bodyTail}
    </>
  ) : (
    A.bodyGeneric
  );
  const content = (
    <div className="flex min-h-[60vh] items-center justify-center">
      <AccessDeniedBody
        title={A.title}
        body={body}
        primary={{ label: A.back, href: signedIn?.home && signedIn.home !== "/access-denied" ? signedIn.home : "/" }}
        requestAccess={Boolean(signedIn)}
        code="403-SCOPE"
        at={at}
        tone="info"
        icon="visibility_off"
      />
    </div>
  );

  if (signedIn && signedIn.organization?.kind === "lender") {
    return <LenderShell user={toShellUser(signedIn)}>{content}</LenderShell>;
  }
  return (
    <>
      <PublicHeader variant="logo" />
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-[640px] px-6 py-10 outline-none">
        {content}
      </main>
    </>
  );
}
