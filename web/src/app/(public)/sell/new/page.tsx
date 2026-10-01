import type { Metadata } from "next";
import { SaleWizard } from "@/components/market/SaleWizard";
import { getCatalog, optionalIndividual } from "@/lib/market/server";
import { PublicPage } from "../../PublicPage";

export const metadata: Metadata = { title: "طلب بيع جديد", robots: { index: false, follow: false } };

/** The first sale request: three short steps; anyone can start, sign-in comes at step 3. */
export default async function NewSaleRequestPage() {
  const [catalog, me] = await Promise.all([getCatalog(), optionalIndividual()]);
  return (
    <PublicPage eyebrow="بيع عقارك" title="طلب بيع جديد" lead="ثلاث خطوات قصيرة. يمكنك الحفظ والعودة، واستكمال الصور والمستندات بعد الإرسال.">
      <div className="max-w-[820px]">
        <SaleWizard catalog={catalog} signedIn={Boolean(me)} userName={me && me.user.name !== "عميل رهون" ? me.user.name : null} />
      </div>
    </PublicPage>
  );
}
