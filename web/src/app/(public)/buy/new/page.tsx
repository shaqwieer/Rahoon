import type { Metadata } from "next";
import { BuyerWizard } from "@/components/market/BuyerWizard";
import { getCatalog, optionalIndividual } from "@/lib/market/server";
import { PublicPage } from "../../PublicPage";

export const metadata: Metadata = { title: "طلب شراء", robots: { index: false, follow: false } };

export default async function NewBuyerRequestPage() {
  const [catalog, me] = await Promise.all([getCatalog(), optionalIndividual()]);
  return (
    <PublicPage eyebrow="للمشترين" title="حدد قدرتك الشرائية" lead="ثلاث خطوات: ما تستطيع دفعه الآن وما تلتزم به لاحقًا، ثم تفضيلاتك. لا نطلب مستندات مالية لمجرد التصفح.">
      <div className="max-w-[820px]">
        <BuyerWizard catalog={catalog} signedIn={Boolean(me)} userName={me && me.user.name !== "عميل رهون" ? me.user.name : null} />
      </div>
    </PublicPage>
  );
}
