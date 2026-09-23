import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.provider.help);

export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.provider.help} icon="help" />;
}
