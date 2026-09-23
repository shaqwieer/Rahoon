import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.owner.home);

/** D02 placeholder — owner home. */
export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.owner.home} icon="space_dashboard" />;
}
