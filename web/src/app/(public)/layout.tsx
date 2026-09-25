import type { Metadata } from "next";
import { getServerDictionary } from "@/lib/i18n/server";

/** Public pages (individual-first landing — B13, legal drafts): indexable, SSR, hreflang ar/en. */
export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return {
    description: t.individual.landing.metaDescription,
    robots: { index: true, follow: true },
    // Locale is cookie-based today; both languages share the URL until /en routes exist.
    alternates: { languages: { ar: "/", en: "/" } },
  };
}

export default function PublicLayout({ children }: LayoutProps<"/">) {
  return <div className="flex min-h-dvh flex-col bg-warm">{children}</div>;
}
