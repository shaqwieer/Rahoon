import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerMetadata } from "@/components/owner/server";
import { ComplaintForm } from "./ComplaintForm";

export const generateMetadata = () => ownerMetadata((c) => c.complaint.title);

/** D13 — new complaint/objection. `?type=objection` (from D05 «أعتقد أن هناك خطأ في المبلغ») preselects the objection. */
export default async function OwnerNewComplaintPage({ searchParams }: PageProps<"/owner/complaints/new">) {
  const sp = await searchParams;
  const { c, shell } = await getOwnerContext();
  const initialType = sp.type === "objection" ? "objection" : "complaint";
  return (
    <OwnerPage shell={shell} title={c.complaint.title} backHref="/owner/help" backLabel={c.back} hideNav active="help">
      <p className="m-0 text-17 leading-7">{c.complaint.intro}</p>
      <ComplaintForm initialType={initialType} />
    </OwnerPage>
  );
}
