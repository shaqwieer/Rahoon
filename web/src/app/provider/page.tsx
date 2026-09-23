import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.provider.assignments);

/** V01 placeholder — assignment inbox. */
export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.provider.assignments} icon="assignment" />;
}
