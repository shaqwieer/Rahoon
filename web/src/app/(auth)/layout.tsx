/** Auth route group: login, MFA, workspace selection and owner invitations. Each page chooses its own frame. */
export default function AuthLayout({ children }: LayoutProps<"/">) {
  return children;
}
