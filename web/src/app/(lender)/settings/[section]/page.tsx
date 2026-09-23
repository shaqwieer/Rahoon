import { notFound } from "next/navigation";
import { SETTINGS_NAV, type SettingsNavKey } from "@/components/shell/nav";
import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

function keyFor(section: string): SettingsNavKey | undefined {
  return SETTINGS_NAV.find((s) => s.href === `/settings/${section}`)?.key;
}

export async function generateMetadata({ params }: PageProps<"/settings/[section]">) {
  const key = keyFor((await params).section);
  return placeholderMetadata((t) => (key ? t.nav.settings[key] : t.nav.lender.settings));
}

/** A02–A07 placeholders; replace each with a dedicated route (`settings/users/page.tsx` …) when built. */
export default async function Page({ params }: PageProps<"/settings/[section]">) {
  const key = keyFor((await params).section);
  if (!key) notFound();
  return <ScreenPlaceholder title={(t) => t.nav.settings[key]} icon="tune" />;
}
