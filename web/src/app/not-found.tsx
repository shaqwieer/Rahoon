import { PublicHeader } from "@/components/shell/Public";
import { SystemState } from "@/components/ui/SystemState";
import { getServerDictionary } from "@/lib/i18n/server";

/** Same wording as S12: never reveal whether a resource exists. */
export default async function NotFound() {
  const { t } = await getServerDictionary();
  return (
    <>
      <PublicHeader variant="back" />
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-[640px] px-6 py-12 outline-none">
        <SystemState kind="forbidden" layout="page" headingLevel={1} title={t.auth.accessDenied.title} body={t.auth.accessDenied.bodyGeneric} action={{ label: t.public.backHome, href: "/" }} />
      </main>
    </>
  );
}
