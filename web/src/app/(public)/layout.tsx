import type { Metadata } from "next";
import { M } from "@/components/market/copy";

/** Public pages: indexable, SSR, Arabic. */
export const metadata: Metadata = {
  description: M.meta.description,
  robots: { index: true, follow: true },
};

export default function PublicLayout({ children }: LayoutProps<"/">) {
  return <div className="flex min-h-dvh flex-col bg-warm">{children}</div>;
}
