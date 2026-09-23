import { placeholderMetadata, ScreenPlaceholder } from "@/components/shell/ScreenPlaceholder";

export const generateMetadata = () => placeholderMetadata((t) => t.nav.agent.cases);

/** J05 placeholder — assigned cases. */
export default function Page() {
  return <ScreenPlaceholder title={(t) => t.nav.agent.cases} icon="folder_open" />;
}
