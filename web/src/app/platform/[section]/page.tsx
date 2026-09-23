import { notFound } from "next/navigation";
import { PLATFORM_NAV, type PlatformNavKey } from "@/components/shell/nav";
import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

function itemFor(section: string) {
  return PLATFORM_NAV.find((i) => i.href === `/platform/${section}`);
}

export async function generateMetadata({ params }: PageProps<"/platform/[section]">) {
  const key: PlatformNavKey = itemFor((await params).section)?.key ?? "ops";
  return placeholderMetadata((t) => t.nav.platform[key]);
}

/** PA02–PA18 placeholders; replace each with a dedicated route when built. */
export default async function Page({ params }: PageProps<"/platform/[section]">) {
  const item = itemFor((await params).section);
  if (!item) notFound();
  return <ScreenPlaceholder title={(t) => t.nav.platform[item.key]} icon={item.icon} />;
}
