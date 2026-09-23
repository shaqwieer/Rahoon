import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { PageHeader } from "@/components/shell/PageHeader";
import type { ImportBatchDetail, ImportBatchListItem } from "@/lib/api/imports";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import { ImportView } from "./ImportView";

export const metadata: Metadata = { title: "استيراد الحالات" };

const STATUSES = ["attention", "duplicate", "error", "ready", "imported", "skipped", "all"] as const;
const PAGE_SIZE = 50;

/** L04 — Bulk import: upload CSV (template v3) → validation with row issues and fixes → duplicate decisions → commit as drafts. */
export default async function ImportPage({ searchParams }: PageProps<"/cases/import">) {
  const sp = await searchParams;
  const str = (k: string) => (typeof sp[k] === "string" ? (sp[k] as string) : "");
  const batchId = /^[0-9a-f-]{36}$/i.test(str("batch")) ? str("batch") : "";
  const status = (STATUSES as readonly string[]).includes(str("status")) ? str("status") : "attention";
  const page = Math.max(1, Number.parseInt(str("page"), 10) || 1);

  const [list, me] = await Promise.all([apiLoad<ImportBatchListItem[]>("/cases/imports"), getMe()]);
  if (!list.ok) {
    return (
      <>
        <PageHeader title="استيراد الحالات" />
        <LoadFailure state={list} forbiddenTitle="لا تملك صلاحية استيراد الحالات" forbiddenBody="الاستيراد الجماعي متاح لمدير الحالات. لم نعرض أي دفعات." />
      </>
    );
  }

  const detail = batchId
    ? await apiLoad<ImportBatchDetail>(`/cases/imports/${batchId}?${new URLSearchParams({ status, page: String(page), pageSize: String(PAGE_SIZE) })}`)
    : null;
  const canCommit = me.authenticated && me.permissions.includes("case.create");

  return (
    <ImportView
      key={`${batchId}|${status}|${page}`}
      batches={list.data}
      detail={detail && detail.ok ? detail.data : null}
      detailFailure={detail && !detail.ok ? detail : null}
      status={status}
      canCommit={canCommit}
    />
  );
}
