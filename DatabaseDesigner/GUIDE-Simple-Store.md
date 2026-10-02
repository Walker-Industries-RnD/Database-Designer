# Building a Simple Store (Inventory + Stripe Payments)

This guide builds a small but real online store with Database Designer: products with stock, orders, and card payments through Stripe. When you're done you'll have an exported ASP.NET Core API that:

- lists and searches products,
- **reserves stock safely** at checkout (two people can never buy the last item),
- creates a Stripe payment for the order,
- marks the order paid **only after Stripe confirms it**, and
- lets a buyer cancel an unpaid order and puts the stock back.

No hand-written backend code is needed; the logic is built in NodeWalker.

> You'll need: Database Designer, PostgreSQL, the .NET 8 SDK (to run the exported API), and a free Stripe account (test mode is enough).

> **Shortcut:** the finished store ships as a project template. In **Projects > Create New Project**, press **Edit Template** and pick **Simple Store** to get the three tables and all five endpoints with their graphs. Read on to see how each part works, or jump to [step 9](#9-check-and-export).

---

## 1. Create the project and tables

Create a new project called **Simple Store**, then add three tables in the **Table Editor**, all in the schema `store`.

> Use lower-case snake_case names (`is_listed`, not `IsListed`). Postgres lower-cases unquoted names, and the build check will warn you if a name would break the API.

### `store.users`

| Column | Type | Settings |
|---|---|---|
| id | BigSerial | Primary |
| username | VarChar, limit `50` | Unique, Not Null |
| email | Text | Not Null |

### `store.products`

| Column | Type | Settings |
|---|---|---|
| id | BigSerial | Primary |
| seller_id | BigInt | Not Null |
| name | VarChar, limit `120` | Not Null |
| price | Numeric, limit `10,2` | Not Null, Check `price >= 0` |
| stock | Integer | Not Null, Default `0`, Check `stock >= 0` |
| is_listed | Boolean | Not Null, Default `true` |

`10,2` means up to 10 digits with 2 after the decimal point, so prices keep their cents. The `stock >= 0` check means the database itself refuses to go below zero.

### `store.orders`

| Column | Type | Settings |
|---|---|---|
| id | BigSerial | Primary |
| buyer_id | BigInt | Not Null |
| product_id | BigInt | Not Null |
| quantity | Integer | Not Null, Check `quantity > 0` |
| total | Numeric, limit `10,2` | Not Null, Default `0` |
| status | VarChar, limit `20` | Not Null, Default `pending` |
| payment_ref | Text | (can be empty; the Stripe payment id goes here) |
| created_at | TimestampTz | Not Null, Default `now()`, answer **Yes** to "Is the default a Postgres function?" |

### References

| Table | Column | References | On Delete |
|---|---|---|---|
| store.products | seller_id | store.users.id | Cascade |
| store.orders | buyer_id | store.users.id | Cascade |
| store.orders | product_id | store.products.id | Restrict |

`Restrict` on `product_id` stops anyone deleting a product that has orders.

Open **ER Diagram** to check it: you should see three cards with arrows from `products.seller_id` and `orders.buyer_id` to `users.id`, and from `orders.product_id` to `products.id`.

> Already have a database? **Import SQL** accepts a `pg_dump --schema-only` file and creates these tables for you.

---

## 2. (Optional) Row-level security

If buyers will connect with their own database role, add RLS in the **RLS Editor**:

- Role **Standard Users**
  - `store.products`: policy *Products*, Access **Public read, own writes**, Owner column `seller_id`
  - `store.orders`: policy *My orders*, Access **Own rows**, Owner column `buyer_id`
- Role **Admin Users**: `store.orders`, Access **Everyone**

Each policy row shows an **Effective** line such as *standard_users may ALL only rows where buyer_id = the current user*. Check it reads the way you mean.

The API in this guide talks to the database as one trusted service account, so RLS is extra protection rather than a requirement. To have requests run under RLS, start each graph with **DB: RLS Set User** (the buyer's id) and **DB: RLS Set Role** (`standard_users`).

---

## 3. Plan the API

In the **API Editor** create module **Store** with two endpoints:

| Endpoint | Function | Verb | Becomes |
|---|---|---|---|
| Products | List | GET | `GET /api/store/products/list` |
| Products | Search | GET | `GET /api/store/products/search?q=` |
| Orders | Checkout | POST | `POST /api/store/orders/checkout` |
| Orders | Confirm | POST | `POST /api/store/orders/confirm?orderId=` |
| Orders | Cancel | POST | `POST /api/store/orders/cancel?orderId=` |

Click **Open in NodeWalker** on each function to build its logic.

### How inputs and the database reach a graph

- A **Custom Input** block becomes an API parameter. Its value `CUSTOMINPUT(long orderId)` means "a `long` called `orderId`". Simple types (`string`, `long` and so on) come from the query string; a table type such as `CUSTOMINPUT(orders order)` is read from the JSON body on POST.
- Leave the **Db** input of database blocks **unconnected**. The API passes in its own database connection. You don't need **DB: Open** in an API graph.
- Tables are referred to by name: a **Type Literal** of `products` means the `store.products` table.
- A **Fail If** block ends the request with `400 Bad Request` and `{ "error": "<your message>" }`.

---

## 4. Products: List

Show listed products, cheapest first.

1. **DB: Query**
   - EntityType <- **Type Literal** `products`
   - Predicate <- **Where: Is True** (Property <- String Literal `IsListed`)
   - OrderBy <- **Select: Field** (Property <- String Literal `Price`)
   - Take <- **Int Literal** `50`
2. **Set Output** <- DB: Query `Rows`

Property names use the C# spelling, PascalCase: `is_listed` becomes `IsListed`.

## 5. Products: Search

1. **Custom Input** = `CUSTOMINPUT(string q)`
2. **Format**: Template <- String Literal `%{0}%`, Arg0 <- Custom Input
3. **Where: Like**: Property <- `Name`, Pattern <- Format `Result` (case-insensitive, `%` = anything)
4. **Where: Is True**: Property <- `IsListed`
5. **Where: And**: A <- Like, B <- Is True
6. **DB: Get Where**: EntityType <- Type Literal `products`, Predicate <- Where: And
7. **Set Output** <- `Rows`

---

## 6. Orders: Checkout

This graph does the important work:

1. Take the stock in one database statement, so it can't oversell.
2. Price and save the order.
3. Ask Stripe to start a payment.
4. Store the payment id on the order and commit.

### Input
- **Custom Input** = `CUSTOMINPUT(orders order)`. The client sends `{"buyerId": 2, "productId": 1, "quantity": 2}`.
- **DB: Begin Tx**: everything below is committed together at the end.
- Two **Expose** blocks on the Custom Input: `ProductId` and `Quantity`.

### 6a. Reserve stock (atomic)
- **DB: Decrement Where**
  - EntityType <- Type Literal `products`
  - Property <- String Literal `Stock`
  - Amount <- Expose `Quantity`
  - Predicate <- **Where: And** of
    - **Where: And** ( **Where: Equals** `Id` = Expose `ProductId`, **Where: Greater Or Equal** `Stock` ≥ Expose `Quantity` ), and
    - **Where: Is True** `IsListed`

  This becomes a single SQL statement: `UPDATE products SET stock = stock - @qty WHERE id = @id AND stock >= @qty AND is_listed`. Two buyers racing for the last item can't both succeed; the second one updates 0 rows.
- **Equals**: A <- Decrement `Affected`, B <- Int Literal `0`
- **Fail If**: Condition <- Equals, Message <- String Literal `Out of stock`

### 6b. Price and save the order
- **DB: Get One By Id**: EntityType <- `products`, Id <- Expose `ProductId`
- **Expose** `Price` on its `Row`
- **Multiply**: A <- Price, B <- Quantity. This is the order total.
- **Run After**: After <- Fail If `Ok`, Value <- Custom Input. This makes sure nothing is saved unless the stock check passed.
- **Set Field**: Object <- Run After `Then`, Property <- `Total`, Value <- Multiply
- **Run After**: After <- Multiply, Value <- the first Run After's `Then`
- **DB: Add And Save**: Entity <- that Run After
- **Expose** `Id` on Add And Save's `Saved` output. That's the new order's id.

`status` is already `pending` (its default) and `created_at` is filled in by the database.

### 6c. Create the Stripe payment
- **HTTP: New Client**
- **Env Var** `STRIPE_SECRET_KEY` -> **HTTP: Set Bearer Token** (Client, Token)
- **Env Var** `STRIPE_API_BASE`; set it to `https://api.stripe.com` on the server
- **Format** (URL): Template `{0}/v1/payment_intents`, Arg0 <- Env Var `STRIPE_API_BASE`
- **Multiply** (cents): A <- order total, B <- Int Literal `100`
- **Format** (form body): Template
  `amount={0:0}&currency=usd&metadata[order_id]={1}&automatic_payment_methods[enabled]=true`
  with Arg0 <- cents, Arg1 <- Expose `Id`
- **HTTP: Post Form**: Client, Url <- URL Format, Form <- body Format. Stripe expects a form-encoded body, not JSON.
- **HTTP: Read JSON Field** ×3 on the response: `error.message`, `id`, `client_secret`
- **Not Equals**: A <- `error.message`, B <- String Literal (empty) -> **Fail If** (Message <- `error.message`). This passes Stripe's own error back to the caller.

> Never put the secret key in the graph. **Env Var** reads it from the server, so it isn't stored in your project or templates.

### 6d. Save the payment id, commit, reply
- **Run After**: After <- the Stripe Fail If `Ok`, Value <- Read JSON Field `id`
- **DB: Update Where**: EntityType <- `orders`, Predicate <- **Where: Equals** `Id` = order id, Property <- `PaymentRef`, Value <- that Run After
- **Run After**: After <- Update Where `Affected`, Value <- DB: Begin Tx `Tx`
- **DB: Commit Tx**: Tx <- that Run After
- **Object: Build**: `orderId` <- order id, `total` <- total, `clientSecret` <- Read JSON Field `client_secret`
- **Run After**: After <- the commit's Run After, Value <- Object: Build
- **Set Output** <- that Run After

The response looks like `{"orderId": 1, "total": 25, "clientSecret": "pi_..._secret_..."}`.

---

## 7. Orders: Confirm

The browser tells your API "I paid", but **your API checks with Stripe itself**. Never trust the browser on this.

1. **Custom Input** = `CUSTOMINPUT(long orderId)`
2. **DB: Get One By Id** (`orders`, Id <- Custom Input)
3. **Equals** (Row, **Null**) -> **Fail If** `Order not found`
4. **Run After** (After <- Fail If `Ok`, Value <- Row) -> **Expose** `PaymentRef`
5. **Format** `{0}/v1/payment_intents/{1}` (Arg0 <- Env Var `STRIPE_API_BASE`, Arg1 <- PaymentRef)
6. **HTTP: New Client** + **Set Bearer Token** (Env Var `STRIPE_SECRET_KEY`) -> **HTTP: Get** (Url <- Format)
7. **HTTP: Read JSON Field** `status` -> **Not Equals** `succeeded` -> **Fail If** `Payment not completed yet`
8. **Run After** (After <- that Fail If `Ok`, Value <- String Literal `paid`)
9. **DB: Update Where**: `orders`, **Where: Equals** `Id` = orderId, Property `Status`, Value <- Run After
10. **Object: Build** `orderId`, `status`, `updated` -> **Set Output**

## 8. Orders: Cancel

Only a `pending` order can be cancelled, and cancelling puts its stock back exactly once.

1. **Custom Input** = `CUSTOMINPUT(long orderId)`
2. **DB: Update Where**: `orders`, Predicate <- **Where: And** (Equals `Id` = orderId, Equals `Status` = `pending`), Property `Status`, Value `cancelled`
3. **Equals** (Affected, `0`) -> **Fail If** `Only pending orders can be cancelled`
4. **Run After** (After <- Fail If `Ok`, Value <- orderId) -> **DB: Get One By Id** (`orders`)
5. **DB: Increment Where**: `products`, **Where: Equals** `Id` = Expose `ProductId`, Property `Stock`, Amount <- Expose `Quantity`
6. **Object: Build** `orderId`, `restocked` -> **Set Output**

Because step 2 only matches a `pending` order, a second cancel (or cancelling a paid order) updates nothing and stops at the Fail If. Stock can't be restocked twice.

---

## 9. Check and export

Open **Build Project** and press **Build Check**. You should see *good to go*, or only tips. Fix anything listed as an error. Then **Export**. The export folder `GeneratedDB/vN` contains:

| File | What it is |
|---|---|
| `SQL.sql` | the schema |
| `RLS.sql` | roles and policies (if you did step 2) |
| `Migration.sql` | upgrade script from your previous export |
| `API/` | the ASP.NET Core project with your NodeWalker logic in `Controllers/Logic` |

Later, when you change the design, export again and run the new `Migration.sql` on your existing database instead of rebuilding it. Anything that would delete data is left commented out for you to review.

## 10. Run it

### From inside Database Designer

Open **Local Database** on the desktop:

1. Enter your Postgres connection (for example `Host=localhost;Port=5432;Username=postgres;Password=yourpassword;Database=shop`) and press **Test & save**. The database is created if it doesn't exist.
2. **Create from scratch** runs `SQL.sql` and `RLS.sql` from your latest export.
3. **Add sample data** fills every table with believable rows (parents before children, so foreign keys line up). Or run your own `INSERT`s in the query box.
4. Set `STRIPE_SECRET_KEY` (and `STRIPE_API_BASE=https://api.stripe.com`) as environment variables, then **Start API**. Swagger opens when it's ready.

You can also test one endpoint without the API: open its graph in NodeWalker and press **▶ Run**. Fill in the inputs (for Checkout: `{"buyerId":2,"productId":1,"quantity":2}`) and you'll see the result, or the error and which block caused it. Database changes are rolled back unless you tick **Keep database changes**, even though Checkout commits its own transaction.

### By hand

```bash
createdb shop
psql shop -f SQL.sql
psql shop -f RLS.sql        # if you added RLS
```

Seed a seller and a product:

```sql
INSERT INTO store.users (username, email) VALUES ('alice', 'alice@example.com'), ('bob', 'bob@example.com');
INSERT INTO store.products (seller_id, name, price, stock) VALUES (1, 'Desk Lamp', 12.50, 3);
```

Then, from the `API` folder:

```bash
export ConnectionStrings__Default="Host=localhost;Database=shop;Username=postgres;Password=yourpassword"
export STRIPE_SECRET_KEY=sk_test_yourkey          # Stripe Dashboard > Developers > API keys (test mode)
export STRIPE_API_BASE=https://api.stripe.com
dotnet run
```

Swagger opens at `/swagger`. Try it:

```bash
curl http://localhost:5000/api/store/products/list
curl -X POST http://localhost:5000/api/store/orders/checkout \
     -H "Content-Type: application/json" -d '{"buyerId":2,"productId":1,"quantity":2}'
```

## 11. Taking the payment in the browser

Checkout returned a `clientSecret`. With Stripe.js:

```html
<script src="https://js.stripe.com/v3/"></script>
<div id="payment"></div><button id="pay">Pay</button>
<script type="module">
  const stripe = Stripe('pk_test_yourkey');                 // publishable key
  const { orderId, clientSecret } = await (await fetch('/api/store/orders/checkout', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ buyerId: 2, productId: 1, quantity: 1 })
  })).json();
  const elements = stripe.elements({ clientSecret });
  elements.create('payment').mount('#payment');
  document.getElementById('pay').onclick = async () => {
    const { error } = await stripe.confirmPayment({ elements, redirect: 'if_required' });
    if (!error) await fetch(`/api/store/orders/confirm?orderId=${orderId}`, { method: 'POST' });
  };
</script>
```

In test mode use card `4242 4242 4242 4242`, any future date, any CVC.

## Where to go next

- **Webhooks:** also call your Confirm logic from a Stripe `payment_intent.succeeded` webhook, so an order is marked paid even if the buyer closes the tab.
- **Expiring holds:** cancel `pending` orders older than, say, 30 minutes. A **DB: Query** with **Where: Less** on `CreatedAt` plus the Cancel steps does it.
- **Sellers:** a `GET orders/for-seller` endpoint with **Where: Equals** on the product's `SellerId`.
- **Several items per order:** an `order_items` table referencing `orders`; checkout repeats step 6a per item inside the same transaction.
