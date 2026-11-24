import type { Customer } from "./types";

/** For example "SGD 49.90". */
export function formatAmount(amount: number, currency: string): string {
  return new Intl.NumberFormat("en-SG", {
    style: "currency",
    currency,
    currencyDisplay: "code",
  }).format(amount);
}

export function customerName(customer: Customer): string {
  return `${customer.firstName} ${customer.lastName}`;
}

// 2C2P channel codes that have a friendlier name. Anything else is shown as the raw code.
const PAYMENT_METHODS: Record<string, string> = {
  CC: "Credit / debit card",
};

export function paymentMethodLabel(channelCode: string | null): string {
  if (!channelCode) {
    return "—";
  }
  return PAYMENT_METHODS[channelCode] ?? channelCode;
}
