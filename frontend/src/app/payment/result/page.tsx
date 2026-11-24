import Link from "next/link";
import { Suspense } from "react";
import PaymentResult from "@/components/PaymentResult";

// Two ways in, both with ?invoiceNo=:
// - the backend's 2C2P return endpoint redirects here after the payment page or 3-D Secure
//   (it first runs a Payment Inquiry if the payment is still Pending);
// - the checkout page comes here directly when a saved-card payment needs no redirect.
export default function PaymentResultPage({ searchParams }: PageProps<"/payment/result">) {
  return (
    // The query string is only known at request time, so it is read inside Suspense
    // (required with Cache Components).
    <Suspense fallback={<p className="text-sm text-gray-500">Loading payment…</p>}>
      {searchParams.then(({ invoiceNo }) => {
        const value = Array.isArray(invoiceNo) ? invoiceNo[0] : invoiceNo;
        return value ? <PaymentResult invoiceNo={value} /> : <MissingInvoice />;
      })}
    </Suspense>
  );
}

function MissingInvoice() {
  return (
    <div className="space-y-4">
      <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        No invoice number was given, so there is no payment to show.
      </p>
      <Link href="/" className="inline-block text-sm font-medium text-blue-600 hover:underline">
        Back to checkout
      </Link>
    </div>
  );
}
