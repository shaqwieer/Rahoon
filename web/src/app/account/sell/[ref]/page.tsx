import { notFound } from "next/navigation";
import { SaleFileView } from "@/components/market/SaleFileView";
import { getCatalog, marketGet } from "@/lib/market/server";
import type { SaleFile } from "@/lib/market/types";

export default async function SaleFilePage({ params }: PageProps<"/account/sell/[ref]">) {
  const { ref } = await params;
  const [file, catalog] = await Promise.all([marketGet<SaleFile>(`/market/sale-requests/${encodeURIComponent(ref)}`), getCatalog()]);
  if (!file) notFound();
  return <SaleFileView key={file.updatedAt} initial={file} catalog={catalog} />;
}
