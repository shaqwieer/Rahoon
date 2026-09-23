import { SystemState } from "@/components/ui";

/** Tab skeleton shown under the CaseHeader while a case tab's server read is in flight (C11 loading). */
export default function CaseTabLoading() {
  return <SystemState kind="loading" />;
}
