import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Checkout",
  description: "Pay with 2C2P: the payment page, or a saved card.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full bg-gray-100 px-4 py-10 text-gray-900">
        <main className="mx-auto w-full max-w-xl rounded-xl bg-white p-6 shadow-sm sm:p-8">{children}</main>
      </body>
    </html>
  );
}
