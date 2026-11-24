import { Suspense } from "react";
import CheckoutForm from "@/components/CheckoutForm";

// The query string can preselect the customer and product (?customerId=1&productId=3). The
// result page's Go back button uses it, so a failed payment can be retried.
export default function CheckoutPage({ searchParams }: PageProps<"/">) {
  return (
    <>
      <h1 className="mb-6 text-2xl font-semibold">Checkout</h1>
      {/* The query string is only known at request time, so it is read inside Suspense and
          the rest of the page can still be prerendered (required with Cache Components). */}
      <Suspense fallback={<p className="text-sm text-gray-500">Loading…</p>}>
        {searchParams.then((query) => (
          <CheckoutForm initialCustomerId={toId(query.customerId)} initialProductId={toId(query.productId)} />
        ))}
      </Suspense>
    </>
  );
}

function toId(value: string | string[] | undefined): number | null {
  const id = Number(Array.isArray(value) ? value[0] : value);
  return Number.isInteger(id) && id > 0 ? id : null;
}
