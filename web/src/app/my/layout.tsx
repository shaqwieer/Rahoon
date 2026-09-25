import { requireIndividualPortal } from "@/lib/api/guards";

/** Individual area (ADR 0001): self-registered individuals only; the API authorizes every call. */
export default async function MyLayout({ children }: LayoutProps<"/my">) {
  await requireIndividualPortal();
  return children;
}
