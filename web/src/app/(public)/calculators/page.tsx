import type { Metadata } from "next";
import { Calculators } from "@/components/market/Calculators";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = {
  title: "الحاسبات",
  description: "حاسبة الخروج من التزام لدى مطور، وحاسبة بيع عقار مموّل، وحاسبة القدرة الشرائية — بقواعد حساب واحدة ونتائج تقديرية.",
};

export default async function CalculatorsPage({ searchParams }: PageProps<"/calculators">) {
  const sp = await searchParams;
  const tab = sp.tab === "financier" || sp.tab === "buyer" ? sp.tab : "developer";
  return (
    <PublicPage wide title="الحاسبات" lead="احسب قبل أن تقرر. الحقول الفارغة تُعامل كقيم غير معروفة، والنتيجة توضح ما ينقصها.">
      <Calculators initial={tab} />
    </PublicPage>
  );
}
