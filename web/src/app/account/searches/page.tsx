import { SavedSearchList } from "@/components/market/discovery/SavedSearchList";
import { apiGet } from "@/lib/api/server";
import type { AlertDelivery, SavedSearchView } from "@/lib/market/types";

export const metadata = { title: "عمليات البحث المحفوظة" };

export default async function SavedSearchesPage() {
  const r = await apiGet<{ items: SavedSearchView[]; max: number; delivery: AlertDelivery }>("/market/my/searches");
  return <SavedSearchList initial={r.items} delivery={r.delivery} max={r.max} />;
}
