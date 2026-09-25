import { SystemState } from "@/components/ui";

/** Complaints list/detail skeleton while the server read is in flight (C11 loading). */
export default function ComplaintsLoading() {
  return <SystemState kind="loading" />;
}
