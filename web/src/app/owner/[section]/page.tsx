import { notFound } from "next/navigation";
import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

const SECTIONS = { documents: "docs", options: "options", payments: "payments", help: "help", messages: "messages" } as const;
type Section = keyof typeof SECTIONS;
const isSection = (s: string): s is Section => s in SECTIONS;

export async function generateMetadata({ params }: PageProps<"/owner/[section]">) {
  const s = (await params).section;
  return placeholderMetadata((t) => (isSection(s) ? t.nav.owner[SECTIONS[s]] : t.nav.owner.home));
}

/** D03–D14 placeholders; replace each with a dedicated route when built. */
export default async function Page({ params }: PageProps<"/owner/[section]">) {
  const s = (await params).section;
  if (!isSection(s)) notFound();
  return <ScreenPlaceholder title={(t) => t.nav.owner[SECTIONS[s]]} icon="construction" />;
}
