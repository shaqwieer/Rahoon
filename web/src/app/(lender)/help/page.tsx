import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.lender.help);

/** Placeholder — replace with the real screen (shell, h1 and empty state only; no fake data). */
export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.lender.help} icon="help" />;
}
