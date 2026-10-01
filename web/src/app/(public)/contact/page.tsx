import type { Metadata } from "next";
import { ContactForm } from "@/components/market/ContactForm";
import { M } from "@/components/market/copy";
import { PublicPage } from "../PublicPage";

export const metadata: Metadata = { title: M.contact.metaTitle };

/** Contact: a real form stored for the Rahoon team (no invented phone numbers or e-mail addresses). */
export default function ContactPage() {
  return (
    <PublicPage title={M.contact.title} lead={M.contact.lead}>
      <ContactForm />
    </PublicPage>
  );
}
