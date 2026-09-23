import { CaseChrome } from "@/components/case/CaseChrome";
import { getWorkspace } from "@/lib/api/case";

/** Every case page shares the CaseHeader (status, SLA, stages, tabs). Visibility is enforced by the API (403 → access denied). */
export default async function CaseLayout({ children, params }: LayoutProps<"/cases/[ref]">) {
  const { ref } = await params;
  const ws = await getWorkspace(ref);
  return <CaseChrome ws={ws}>{children}</CaseChrome>;
}
