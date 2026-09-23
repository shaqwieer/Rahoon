import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.agent.help);

export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.agent.help} icon="help" />;
}
