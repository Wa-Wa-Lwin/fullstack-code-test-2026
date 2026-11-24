# fullstack-code-test-2026

A checkout backend that takes payments through the **2C2P** payment gateway (PGW v4.3): one-time payments on the 2C2P payment page, recurring products that save the card, and payments with a saved card. It is built with .NET 10 / ASP.NET Core, MySQL 8.4 and Redis 7, with a small Next.js checkout page in front.

**Contents:** [Setup](#setup) · [2C2P credentials](#2c2p-credentials) · [API](#api) · [Project structure](#project-structure) · [Payment flows](#payment-flows) · [Idempotency, locking and caching](#idempotency-locking-and-caching) · [Seed data](#seed-data) · [Database changes](#database-changes) · [Assumptions](#assumptions) · [What I would improve](#what-i-would-improve)

## Setup

You need Docker. To run the API outside Docker you also need the .NET 10 SDK.

### Option 1: everything in Docker

```sh
cp .env.example .env          # fill in TWOC2P_MERCHANT_ID and TWOC2P_SECRET_KEY
docker compose up -d --build  # MySQL, Redis and the API
```

The API listens on http://localhost:8080, with Swagger UI at http://localhost:8080/swagger. It waits for MySQL and Redis to be healthy, then applies the migrations and seed data on startup.

### Option 2: API with `dotnet run`, databases in Docker

```sh
docker compose up -d --wait mysql redis     # MySQL on host port 3307, Redis on 6379
cd backend
cp PaymentIntegration.Api/appsettings.Development.example.json PaymentIntegration.Api/appsettings.Development.json
                                            # then fill in the 2C2P merchant ID and secret
dotnet run --project PaymentIntegration.Api # http://localhost:5263, Swagger UI at /swagger
```

In Development, the API applies any pending EF Core migrations when it starts, so the first run creates the tables and the seed data below. In production, migrations would run as a separate deployment step instead, so that several app instances never migrate the same database at once.

`PaymentIntegration.Api/PaymentIntegration.Api.http` has sample requests.

### Frontend

The checkout page is a Next.js app (Node.js 20.9 or later). Start the API first, then:

```sh
cd frontend
npm install
npm run dev      # http://localhost:3000
```

Next.js forwards `/api/*` to the API at `API_BASE_URL` (default `http://localhost:5263`). For the API running in Docker, copy `frontend/.env.example` to `frontend/.env.local` and set `API_BASE_URL=http://localhost:8080` before starting. `frontend/README.md` has the details.

### Tests

```sh
cd backend
dotnet test PaymentIntegration.slnx
```

The integration tests start their own MySQL and Redis containers with Testcontainers, so Docker must be running. They never touch the development database. 2C2P is replaced by a fake gateway in the integration tests and by a fake HTTP handler in the gateway unit tests, so no test calls the sandbox.

- **Unit:** JWT signing and verification (a tampered or wrongly signed payload is rejected) and the gateway's mapping of 2C2P requests, responses and codes.
- **Idempotency:** concurrent checkouts with one key create one transaction and one gateway call, with and without the Redis lock; a repeat after completion returns the stored transaction.
- **Notifications:** a repeated notification (also ten at once) saves one card and succeeds once; a failure after success does not overturn it; a bad signature or a different amount changes nothing.
- **Validation:** a missing `Idempotency-Key` (400), an unknown customer or product (404), another customer's card (422).

### Trying a sandbox payment from Swagger

1. Open http://localhost:5263/swagger and call `POST /api/checkout` with any UUID in the `Idempotency-Key` header and the body `{ "customerId": 1, "productId": 1 }` (product 3 or 4 for a recurring product that saves the card).
2. Open the returned `redirectUrl` in a browser and pay with a 2C2P sandbox test card.
3. 2C2P returns the browser to the API, which runs a Payment Inquiry and redirects to the frontend result page (start the frontend to see it, or ignore the error page).
4. Call `GET /api/transactions/{invoiceNo}` to see the stored status. After a recurring product, `GET /api/customers/1/cards` lists the saved card, and checkout with `"customerCardId": <id>` charges it directly.

Swagger's "Try it out" cannot complete the payment itself: the 2C2P page and the 303 redirect from the return endpoint need a real browser.

## 2C2P credentials

Use the 2C2P sandbox merchant ID, secret key and the Singapore test cards from the 2C2P sandbox page.

**`dotnet run`:** copy the template and fill in the two values.

```sh
cd backend/PaymentIntegration.Api
cp appsettings.Development.example.json appsettings.Development.json
```

```json
{
  "TwoC2P": {
    "MerchantId": "<your-2c2p-merchant-id>",
    "SecretKey": "<your-2c2p-merchant-secret>"
  }
}
```

**Docker:** copy `.env.example` to `.env` and set `TWOC2P_MERCHANT_ID` and `TWOC2P_SECRET_KEY`. Compose passes them to the API as `TwoC2P__MerchantId` and `TwoC2P__SecretKey`. Any setting can also be overridden with an environment variable of that form, or with `dotnet user-secrets`.

| Setting | Where | Default |
| --- | --- | --- |
| `TwoC2P:MerchantId`, `TwoC2P:SecretKey` | git-ignored `appsettings.Development.json` or `.env` | none (required for a checkout) |
| `TwoC2P:BaseUrl` | `appsettings.json` | `https://sandbox-pgw.2c2p.com/payment/4.3/` |
| `TwoC2P:FrontendReturnUrl` | `appsettings.json` | `http://localhost:5263/api/payments/2c2p/return/{invoiceNo}` |
| `TwoC2P:BackendReturnUrl` | `appsettings.json` or `.env` | `http://localhost:5263/api/payments/2c2p/notification` |
| `Frontend:PaymentResultUrl` | `appsettings.json` | `http://localhost:3000/payment/result` |

- **Merchant ID and secret key** are secrets. They go only in the git-ignored `backend/PaymentIntegration.Api/appsettings.Development.json`, or in the git-ignored `.env` for Docker. They are never committed.
- **`TwoC2P:BaseUrl`** (in the committed `appsettings.json`) includes the API version: `https://sandbox-pgw.2c2p.com/payment/4.3/`. Changing the version is a config change.
- **`TwoC2P:BackendReturnUrl`** is where 2C2P posts the payment notification. 2C2P cannot reach `localhost`. To receive notifications locally, expose the API with `ngrok http 5263` (or `8080` for Docker) or a Cloudflare Tunnel, and set this URL to `https://<tunnel>/api/payments/2c2p/notification`.
- **`TwoC2P:FrontendReturnUrl`** is where 2C2P sends the customer's browser back. It points at the API, not at the frontend (see [Payment flows](#payment-flows)). `{invoiceNo}` in it is replaced per payment.

Use the `http` launch profile locally. Outside Development the API redirects HTTP to HTTPS, and a redirect drops the body of 2C2P's notification POST.

The 2C2P settings are checked on the first payment, not at startup. The customer, product and transaction endpoints work without them, and a checkout without them fails with a message naming the missing setting.

## API

| Method | Path | What it does |
| --- | --- | --- |
| GET | `/api/customers` | Customers (cached) |
| GET | `/api/customers/{id}/cards` | A customer's saved cards, masked (cached, cleared when a card is saved) |
| GET | `/api/products` | Products (cached) |
| POST | `/api/checkout` | Starts a payment. Requires an `Idempotency-Key` header. |
| GET | `/api/transactions/{invoiceNo}` | A transaction's status for the result page. Always read from the database. |
| POST | `/api/payments/2c2p/notification` | 2C2P's backend notification (`{"payload": "<JWT>"}`) |
| GET, POST | `/api/payments/2c2p/return/{invoiceNo}` | Where 2C2P returns the browser. Redirects to the frontend result page. |

Errors are problem-details responses: 400 for invalid input or a missing `Idempotency-Key`, 404 for an unknown resource, 409 (with `Retry-After`) while a checkout with the same key is in progress, 422 for a card of another customer or a key reused for a different checkout, and 502 when 2C2P cannot be used.

## Project structure

```
docker-compose.yml                    MySQL, Redis and the API (reads secrets from .env).
backend/
  PaymentIntegration.slnx
  PaymentIntegration.Domain/          No dependencies.
    Entities/                         Customer, CustomerCard, Product, Transaction (status changes only via MarkSucceeded/MarkFailed).
    Enums/                            PaymentType, TransactionStatus.
  PaymentIntegration.Application/     Use cases and ports. Knows nothing about 2C2P, MySQL or Redis.
    Services/                         CheckoutService (idempotent checkout), PaymentNotificationService,
                                      PaymentResultService (applies any result: notification, inquiry or return),
                                      TransactionService, and the cached reads CustomerService and ProductService.
    Interfaces/                       Repositories, IPaymentGateway, ICacheService, IDistributedLockService.
    DTOs/, DTOs/Payments/             API shapes, and the provider-neutral gateway requests and results.
    Caching/CacheKeys.cs              Every cache key is built here.
    Exceptions/                       NotFound (404), Validation (400), Conflict (409), BusinessRule (422), PaymentGateway (502).
  PaymentIntegration.Infrastructure/  Implementations of the ports.
    Persistence/                      PaymentDbContext, entity configurations, migrations, seed data.
    Repositories/                     EF Core repositories, including the conditional "update only if Pending".
    Redis/                            RedisCacheService (failures are a cache miss) and RedisDistributedLockService.
    Payments/                         TwoC2PPaymentGateway, the JWT helper, request/response messages, response codes.
    DependencyInjection.cs            Registers all of the above, including the 2C2P HttpClient (15s timeout).
  PaymentIntegration.Api/             Controllers, ApiExceptionHandler (exceptions → problem details), Program.cs.
  PaymentIntegration.Tests/           Unit/ (JWT, gateway) and Integration/ (Testcontainers MySQL + Redis).
frontend/                             Next.js checkout page and result page (calls the API through its /api proxy).
```

Dependencies point inwards: Api → Application + Infrastructure, Infrastructure → Application → Domain. The Application layer talks to 2C2P only through `IPaymentGateway`, whose types are provider-neutral, so all JWT handling, endpoint paths and 2C2P response codes stay in `TwoC2PPaymentGateway`. A second provider would be another implementation of the interface.

## Payment flows

**One-time product.** Checkout creates a Pending transaction, asks 2C2P for a Payment Token and returns the 2C2P payment page URL. The customer pays there. 2C2P then posts the result to the notification endpoint, which verifies its signature and finalises the transaction.

**Recurring product.** The same, but the Payment Token request is card-only with `tokenize: true`. The successful result carries a `customerToken`, which is saved as a card for the customer. Whichever result finalises the payment saves it: the notification, or the Payment Inquiry run on the browser return. Both go through `PaymentResultService`, so the rules live in one place.

**Saved card.** Checkout charges the card directly: Payment Token (with `customerToken` and `request3DS: "N"`), then Do Payment.
- Code `2000`: the charge finished. Payment Inquiry runs at once and the response is final, with no redirect URL.
- Code `1001`: the issuer wants 3-D Secure anyway. The response is Pending with the 3DS URL.
- Any other code: Failed.

**Browser return.** 2C2P sends the browser back to `/api/payments/2c2p/return/{invoiceNo}`, which accepts GET and POST because a payment page may return the browser with a form POST that a Next.js page cannot receive. The endpoint ignores what the browser brings: if the transaction is still Pending, it runs a Payment Inquiry, which is how a saved card's result after 3-D Secure is fetched. It then redirects to the frontend result page.

## Idempotency, locking and caching

### Idempotency: three guards that back each other up

The client sends an `Idempotency-Key` header with every checkout (a new UUID per purchase attempt, reused on retries). The rule is that a key produces at most one transaction and one 2C2P call. `CheckoutService` enforces it in layers:

1. **Stored result.** If a transaction with the key exists, its stored invoice number, status and redirect URL are returned without calling 2C2P. This covers the common case: a client retrying after a timeout.
2. **Redis lock `lock:checkout:{key}`.** Only one request per key does the work at a time. A second concurrent request gets 409 with `Retry-After`. After taking the lock, the service looks the key up again, because the first request may have finished in between.
3. **Unique index on `idempotency_key`.** The Pending transaction is inserted and saved *before* 2C2P is called. If two requests get past the lock (it expired, or Redis is down), only one insert succeeds. The other sees the duplicate-key error, loads the winner's row and returns it (or 409 while it is still in progress). A request that loses the insert never calls 2C2P.

Reusing a key for a different customer, product or card returns 422 instead of somebody else's transaction.

Why all three: the stored result alone races (two requests both see "no transaction yet"); the lock alone fails when Redis is down or a 2C2P call outlives it; the unique index alone works but would let both requests reach the database insert. Together, the database is the final authority and Redis only reduces contention.

### The lock

`SET key <owner-guid> PX 60000 NX` to acquire, and a Lua compare-and-delete to release, so a request only ever deletes a lock it still owns. The TTL (60s) is longer than the longest work done under the lock: a saved card makes up to three 2C2P calls in a row, each with a 15s timeout. If Redis is down, checkout logs the error and continues without the lock, relying on the unique index.

### Final status never changes

A transaction's status changes only through `MarkSucceeded`/`MarkFailed`, which act only while it is Pending, and the database write is a single `UPDATE ... WHERE id = @id AND status = 'Pending'`. When the notification and a Payment Inquiry race, exactly one of them updates the row; a repeated or late notification changes nothing. Every result also has to match the stored amount and currency, or it is logged and ignored.

Do Payment is never retried (there is no retry handler on the 2C2P HttpClient), because retrying a charge can charge twice. A call whose outcome is unknown (a timeout, or a failed inquiry after a completed charge) leaves the transaction Pending for the notification to settle, rather than marking a possibly paid transaction Failed.

### Caching

Cache-aside in Redis for the customer list, the product list (10 minutes each) and each customer's saved cards (`customer:{id}:cards`, 5 minutes). These are read on every checkout page load and rarely change, so they are the reads worth caching. A cache failure is logged and treated as a miss, so reads fall back to MySQL: Redis makes the app faster but is never required for it to be correct. The TTL bounds how stale an entry can get if an invalidation is ever missed. When a successful result saves a card (through `CustomerService.SaveCardAsync`), it writes MySQL first and then deletes `customer:{id}:cards`, so the next read loads the new card. The card is saved even when the status update changed nothing, so a repeated notification can still save a card whose first save failed, but only for a transaction that is stored as Succeeded.

Transaction status is never cached: it changes whenever the notification arrives, and the result page must show the current value.

## Seed data

There are no endpoints for creating customers or products, so the migrations insert these rows with fixed IDs (`SeedData.cs`; the `RenameSeedData` migration changed the original rows in place, so existing databases keep their transactions and saved cards).

| ID | Customer | Email |
| --- | --- | --- |
| 1 | Lisa La | lisa.la@example.com |
| 2 | Rose Park | rose.park@example.com |
| 3 | Jennie Kim | jennie.kim@example.com |
| 4 | Jisoo Kim | jisoo.kim@example.com |

| ID | Product | Price | Payment type |
| --- | --- | --- | --- |
| 1 | Bangkok Tour Ticket | SGD 188.00 | OneTime |
| 2 | Square One Album | SGD 29.90 | OneTime |
| 3 | Monthly Live Show | SGD 9.99 | Recurring (saves the card) |
| 4 | Yearly Photocard | SGD 99.00 | Recurring (saves the card) |

## Database changes

To add a migration after changing the model:

```sh
cd backend
dotnet tool restore     # installs the pinned dotnet-ef version
dotnet ef migrations add <Name> -p PaymentIntegration.Infrastructure -s PaymentIntegration.Api -o Persistence/Migrations
```

### Differences from the spec's minimum schema

The spec lists the minimum columns. Where the schema differs, this is why:

- **Table names are plural** (`customers`, `products`, `transactions`, `customer_cards`), a common convention that also avoids `transaction`, which is a SQL keyword. Column names are snake_case as in the spec.
- **`status` and `payment_type` are `varchar(20)`** holding the enum name, not MySQL `ENUM`. Adding a value is then a code change, not an `ALTER TABLE` that rewrites the table, and EF Core maps it directly.
- **Some columns are wider than the minimum**, so a longer value from 2C2P is stored rather than rejected: `provider_ref` 100, `payment_channel` 50, `resp_code` 50, `resp_desc` 500, `masked_card_no` 32, `customer_token` 255; `products.name` 200 and `description` 1000.
- **Money** is `decimal(12,2)`, currency is `char(3)` defaulting to `SGD`, and every timestamp is stored in UTC.
- **Indexes:** unique on `transactions.invoice_no`, `transactions.idempotency_key`, `customers.email` and `customer_cards (customer_id, customer_token)`. The foreign keys from `transactions` are `RESTRICT`, so payment history is never deleted with a customer or product.
- **No card data** beyond the 2C2P customer token and the masked number. The token is never returned by the API.

### Columns added to `transactions`

| Column | Type | Why it was added |
| --- | --- | --- |
| `payment_token` | varchar(512), nullable | The token 2C2P returns from its Payment Token API. The spec asks for it to be stored. It is null until 2C2P has returned one. |
| `redirect_url` | varchar(2048), nullable | The 2C2P payment page URL that comes back with the token (or the 3-D Secure URL for a saved card). A repeat checkout request with the same `Idempotency-Key` returns this stored URL instead of calling 2C2P a second time. |
| `customer_card_id` | int, nullable, foreign key to `customer_cards` | The saved card used for the payment. It is null when the customer did not pick a saved card. |

### Status rules

A transaction starts as `Pending`. Its status changes only through two methods on the entity:

- `MarkSucceeded(...)` sets the status to `Succeeded`, records the 2C2P result fields and sets `PaidAt`.
- `MarkFailed(...)` sets the status to `Failed` and records the 2C2P result fields.

Both methods act only while the status is `Pending`. After a transaction becomes `Succeeded` or `Failed`, it never changes. A later call returns `false` and leaves the record as it is, so a duplicate or late 2C2P notification cannot overturn a final result. `Status`, `PaidAt` and the result fields (`RespCode`, `RespDesc`, `ProviderRef`, `PaymentChannel`, `MaskedCardNo`) have private setters, so no other code can change them directly.

## Assumptions

- **API version.** The v4.3 documentation pages list `/payment/4.5/...` URLs, while the v4.3 API reference uses `/payment/4.3/...`. Both paths answer in the sandbox; the code uses 4.3, and the version is part of `TwoC2P:BaseUrl`.
- **Endpoints.** The spec's suggested endpoints are used as they are. One is added: `/api/payments/2c2p/return/{invoiceNo}` is the `frontendReturnUrl`. 2C2P may return the browser with a form POST, which a Next.js page cannot receive, and the saved-card flow must run Payment Inquiry when the customer comes back from 3-D Secure. The endpoint does both, then redirects to the frontend result page, which reads the status from `GET /api/transactions/{invoiceNo}`.
- **Codes other than 0000.** 2C2P codes meaning "still in progress" (`0001`, `2001`) or "no record yet" (`2002`, `4071`) leave the transaction Pending rather than Failed, and a Payment Inquiry that 2C2P could not answer (a system error) changes nothing. Every other code marks it Failed. Marking Failed too early would be permanent, because a final status never changes, so a later success notification would be ignored.
- **Saved-card field name.** Do Payment sends the saved card as `payment.data.customerToken`, as the server-side parameter table says. The mobile SDK guide uses `token`; the first sandbox run with a saved card confirms which one 2C2P expects.
- **Amount.** The amount and currency come from the product at checkout time; one checkout buys one product.
- **Cards** are saved only from a successful payment that returns a `customerToken`. A declined payment never saves a card.
- **Unknown outcomes stay Pending.** When 2C2P cannot tell whether a charge happened, the transaction waits for the notification instead of being marked Failed.
- **No authentication.** The customers are seeded, and any client can act for any customer. A real system would derive the customer from the signed-in user.

## What I would improve

- **Reconciliation job.** A background job that runs Payment Inquiry for transactions still Pending after a few minutes, so a lost notification (or a tunnel that was down) cannot leave a payment Pending for good.
- **Outbox for side effects.** Card saving and cache invalidation run after the status update; an outbox table would make them retryable and exactly-once instead of relying on 2C2P repeating the notification.
- **Audit trail.** Store every raw, verified notification and inquiry response, for disputes and debugging.
- **Lock renewal.** Extend the lock while a slow 2C2P call is still running, instead of relying on a fixed TTL.
- **Security and operations.** Authentication, rate limiting on checkout, secrets in a secret store, health checks, and metrics and tracing around the 2C2P calls.
- **Safe retries.** Payment Inquiry is a read and could be retried with backoff (Do Payment must never be).
