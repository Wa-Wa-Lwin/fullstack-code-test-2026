# Checkout frontend

Next.js 16 (App Router), React 19 and Tailwind CSS 4. It needs Node.js 20.9 or later.

## Run it

Start the .NET API first (it listens on `http://localhost:5263`), then:

```sh
npm install
npm run dev      # http://localhost:3000
```

Other scripts: `npm run build`, `npm start` (serves the build) and `npm run lint`.

## Configuration

The browser only calls this app. Next.js forwards every `/api/*` request to the .NET API (a rewrite in `next.config.ts`), so the API needs no CORS setup. `API_BASE_URL` sets where the requests go, and defaults to `http://localhost:5263`. To change it, copy `.env.example` to `.env.local`. The rewrite is fixed when `next dev` starts or when the app is built, so set the variable before either.

## Pages

| Path | What it does |
|---|---|
| `/` | Checkout: pick a customer and product, then a saved card or a new payment method. `?customerId=&productId=` preselects both. |
| `/payment/result?invoiceNo=` | Result page. It reads the status from the API and asks again every 3 seconds while it is Pending. |

## Coming back from 2C2P

The frontend never talks to 2C2P. After the payment page or 3-D Secure, 2C2P sends the browser to the backend (`/api/payments/2c2p/return/{invoiceNo}`). The backend runs a Payment Inquiry if the payment is still Pending, then redirects to `/payment/result?invoiceNo=…`. A saved-card payment that needs no 3-D Secure goes straight from checkout to the result page.

## Duplicate payments

The checkout page creates an `Idempotency-Key` (a UUID) when it loads and sends it with `POST /api/checkout`. It creates a new key only when the customer, product or card changes, because that is a different purchase. A double click, a retry after an error, or a 409 ("still in progress", retried after `Retry-After`) reuses the same key, so the API returns the first result instead of charging again. The Checkout button is also disabled while a request is in flight.

The result page shows the status stored by the API, never the status in 2C2P's redirect, because the customer can come back before 2C2P's backend notification has arrived.
