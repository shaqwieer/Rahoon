"use client";

import { useRouter } from "next/navigation";
import { SystemState } from "@/components/ui";

/**
 * C11 state for a tab or list whose read failed: «forbidden» (role lacks the permission — nothing about the data is shown)
 * or «error» with the ERR reference and a retry that re-runs the server read.
 */
export function LoadFailure({ state, forbiddenTitle, forbiddenBody }: {
  state: { kind: "forbidden" | "error"; errorRef: string; title: string | null };
  forbiddenTitle?: string;
  forbiddenBody?: string;
}) {
  const router = useRouter();
  if (state.kind === "forbidden") {
    return (
      <SystemState
        kind="forbidden"
        title={forbiddenTitle ?? "لا تملك صلاحية عرض هذا القسم"}
        body={forbiddenBody ?? "دورك لا يتيح الاطلاع على هذه البيانات. لم نعرض أي بيانات منها."}
      />
    );
  }
  return <SystemState kind="error" errorRef={state.errorRef} body={state.title ?? undefined} action={{ onClick: () => router.refresh() }} />;
}
