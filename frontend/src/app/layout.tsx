import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Checkout",
  description: "Pay with 2C2P: the payment page, or a saved card.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full bg-gray-100 text-gray-900">
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto flex h-14 w-full max-w-xl items-center justify-between px-4">
            <Link href="/" className="text-lg font-semibold text-gray-900">
              Payment Checkout
            </Link>
            <span className="text-sm text-gray-500">Payments by 2C2P</span>
          </div>
        </header>
        <main className="mx-auto w-full max-w-xl px-4 py-10">
          <div className="rounded-xl bg-white p-6 shadow-sm sm:p-8">{children}</div>
        </main>
      </body>
    </html>
  );
}
