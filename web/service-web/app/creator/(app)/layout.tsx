import { AuthGuard } from "@/components/layout/auth-guard";
import { Sidebar } from "@/components/layout/sidebar";
import { Topbar } from "@/components/layout/topbar";
import { ApiProviders } from "@/providers/app-providers";

export default function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ApiProviders>
      <AuthGuard>
        <div className="flex min-h-screen w-full">
          <Sidebar />
          <div className="flex min-w-0 flex-1 flex-col">
            <Topbar />
            <main className="flex-1 overflow-y-auto px-4 py-6 md:px-8 md:py-8">
              {children}
            </main>
          </div>
        </div>
      </AuthGuard>
    </ApiProviders>
  );
}
