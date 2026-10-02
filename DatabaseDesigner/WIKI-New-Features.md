# Database Designer 3.0: New Features

What changed in 3.0, grouped by area. Copy whatever you need into the public wiki.

Menu paths are written as **Window > Button**.

---

## Projects

### Portable export and import
Projects are saved encrypted with your account, so they can't be moved between machines or accounts as they are. **Export (Portable)** on a project's card in **Projects** writes an unencrypted copy anyone can import:

- A folder in `DatabaseDesignerData\<your username>\Exports\<Project Name>\` with `session.json`, the project's `Scripts\` folder (your NodeWalker functions and their layout), the project icon and the project's wallpaper if it has one.
- A single `<Project Name>.ddproject` file with the same contents.

To import, put the folder or the `.ddproject` file in `DatabaseDesignerData\<your username>\Imports\` and press **Projects > Import Project**. Each one becomes a new project, encrypted for your account. Imported items are moved to `Imports\Imported\`, so pressing Import twice doesn't make duplicates, and an imported copy never overwrites the original project. Name clashes get a number added.

Your local database connection (see Local Database) is never included in an export.

### Undo and redo
On the desktop, **Ctrl+Z** undoes and **Ctrl+Y** (or Ctrl+Shift+Z) redoes changes to the project: tables, references, indexes, RLS, API and notes. Each project keeps up to 100 steps. Moving windows doesn't count as a change. When you undo, windows that show project data close so nothing keeps editing the old copy; help, themes and music stay open.

Inside NodeWalker the same keys undo and redo changes to the graph. Dragging a node counts as one step.

### Notes
**Notes** keeps notes inside the project: search, pinning, an optional linked table, and checklists (lines starting with `[ ]` or `[x]`). Notes are encrypted with the project and are included in portable exports. The note text box fills the window and scrolls.

---

## Tables

### Picking a foreign key target
When you add or edit a reference, you pick the target in two columns: tables on the left, that table's columns on the right. Only columns a foreign key can point at (the primary key, or a column marked Unique) can be picked, and a column whose type doesn't match yours is flagged with the type you need. Editing an existing reference shows what it currently points at.

### ER Diagram
**ER Diagram** shows every table as a card with its columns (PK, FK and UQ marked) and an arrow for each foreign key. Drag cards by their title to arrange them (the layout is saved with the project), use **Auto layout** to reset, zoom with the minus and plus buttons, and click a title to open the table.

**Export SVG** and **Export PNG** save the diagram to `Exports\ER Diagrams` and open that folder.

### Import SQL
**Import SQL** turns an existing Postgres schema into project tables. Paste the SQL or load a `.sql` file (a `pg_dump --schema-only` file works). It reads tables, columns, defaults, primary keys, foreign keys, unique and check constraints, indexes and comments. Functions, triggers, grants and data are skipped and listed so you can see what was left out. The SQL box fills the window and scrolls.

### Build check
**Build Project > Build Check** looks at the whole project. Errors would break SQL.sql or the API, warnings are likely mistakes, and tips are worth a look. Click a line to open that table. It catches:

- duplicate tables or columns, and tables without a primary key
- reserved words used as names (`order`, `user` and so on) and column names with capital letters
- foreign keys whose types don't match, that point at a column that isn't unique, or that use ON DELETE SET NULL on a Not Null column
- foreign key cycles and indexes on columns that don't exist
- RLS policies for tables that don't exist or that have no owner column

---

## Templates

### Table templates (row templates)
**To make one:** open **Build Project > Export Specific Rows**.

1. **Templates** tab: pick the tables to include. Each picked table shows up on the right, where you can give it a description.
2. **Pack Information** tab: a banner (optional), the template name and an overview.
3. **Author** tab: your name, plus optional picture, company, website, license and note.
4. **Finalize** tab: check the preview and press **Finalize**.

The list on the Finalize tab says what's still missing and updates as you type. A name can't contain `\ / : * ? " < > |`. Templates are saved to `DatabaseDesignerData\<your username>\Row Templates\<Name>\v1\`; building the same name again makes v2, v3 and so on, and the newest version is the one offered.

**To use one:** in **Create Table**, press **Use A Template**, then **Select Template Pack** under the pack you want. Its tables are added to the open project. Tables that already exist in the project are left as they are, and you're told which ones.

### Project templates
**To make one:** **Build Project > Export Template**, pick the project, fill in the same information as above and press **Finalize**. Saved to `Project Templates\<Name>\`.

**To use one:** **Projects > Create New Project > Edit Template**, then pick the template. Project templates include the RLS and API settings and the NodeWalker graphs (`Scripts\`).

### Templates that come with the app
These are installed the first time you log in. Installing never overwrites a template you've edited.

| Kind | Template | Tables |
|---|---|---|
| Table | User Accounts | `users` |
| Table | Blog Posts | `posts` |
| Table | Inventory Items | `items` |
| Project | Secure Sign-In | `users`, `sessions` |
| Project | Simple Chat | `users`, `messages` |
| Project | Simple Marketplace | `users`, `items`, `orders` |
| Project | Simple Store | `users`, `products`, `orders`, plus five API endpoints with their graphs |

Templates made before the portable-export update may not import cleanly. If one gives you trouble, delete it from `Row Templates\` or `Project Templates\` and export it again.

---

## NodeWalker

### Flow (exec) wires
Blocks that do something have white pins: **In** on the left and **Then** on the right. Data wires say where a value comes from. Exec wires say which block runs next.

- **Branch** runs the blocks wired to **True** when the condition is true, and the ones wired to **False** otherwise. **After** runs once either side has finished. If both sides lead to the same block, that block runs after the if/else.
- **Sequence** runs Step 1, Step 2, Step 3 and Step 4 in order. Later steps can use values made by earlier ones.
- **For Each** runs **Loop Body** once for every item in Items, with **Item** and **Index**. **Repeat** runs Loop Body Count times, with **Index**.
- **Try** runs the blocks on Try. If anything there throws (Fail If included), it runs Catch instead and gives you the **Error** message.
- **Return** stops and returns the outputs set so far.
- **Then** on an ordinary block fixes the order of steps that don't pass data to each other.

Graphs without exec wires work exactly as before. Graphs that come from a template are laid out left to right the first time you open them. Value blocks (math, comparisons, Where:, Get Variable) are worked out right where they are used, so a Get Variable inside a loop reads the latest value each time round.

An exec output leads to one block (use Sequence for more), but several paths can lead into the same block. Exec pins only connect to exec pins. If a block reads a value that only exists on another path, **View Code** shows a warning above the code.

### Comparisons
Equals, Not Equals, Less Than, Greater Than, Less Or Equal and Greater Or Equal now compare any two values sensibly: numbers by value (an int and a decimal compare fine), text that holds a number as that number, dates as dates, and everything else as text. A null is smaller than everything and only equals another null. Before, mixing types could throw "Object must be of type Double". Rebuild to pick up the fix in older graphs.

### Run
**Run** in the NodeWalker toolbar runs the graph you're editing against your Local Database, using the tables from your latest export. Fill in the inputs and you get the result as JSON, or the error and the name of the block that failed. Changes to the database are rolled back unless you tick **Keep database changes**, and that includes graphs that commit their own transaction. Run needs the .NET SDK and a database set up in Local Database.

### Lambda Logic and List Logic
The freeform Lambda block is replaced by explicit blocks, so you build query logic visually instead of typing `x => ...`:

- **Where:** Equals, Not Equals, Greater, Less, Greater Or Equal, Less Or Equal, Contains, Starts With, Ends With, Like, Is Null, Is Not Null, Is True, Is False, Between, In, and And/Or/Not to combine them.
- **Select: Field** picks the column to sort by.
- **DB: Query** filters, sorts and pages in one block.
- **List:** Filter, First Where, Any Where, Count Where, Sort By, Map Field and Sum Field work on lists you already have.

### Store and API blocks
**Fail If** stops the request with a message (the API answers 400 Bad Request). **Env Var** reads a setting such as an API key, so keys don't live in your graphs. Also new: Set Field, Object: Build, DB: Update Where, DB: Increment Where and DB: Decrement Where (each a single atomic update), HTTP: Post Form, HTTP: Read JSON Field, HTTP: New Request and List Literal.

`CUSTOMINPUT(long orderId)` gives a Custom Input a name as well as a type.

---

## Exporting and running the backend

**Build Project > Export** writes `GeneratedDB\vN\`:

| File | What it is |
|---|---|
| `SQL.sql` | the tables |
| `RLS.sql` | roles, row level security policies and helper functions; safe to run more than once |
| `Migration.sql` | upgrades a database built from the previous export |
| `API\` | an ASP.NET Core project with your NodeWalker logic, Entity Framework models and Swagger |

### Migrations
Each export saves `schema.snapshot.json`, and the next export writes `Migration.sql`: the ALTER statements that bring a database built from the previous export up to date (new tables and columns, type, default, Not Null, Unique and Check changes, foreign keys and indexes). It runs in one transaction. Anything that would delete data (dropped tables or columns) is written but commented out, because a rename looks the same as a drop plus an add. Read it and uncomment what you mean.

### The API
- Models are generated from your tables, and each API function with a NodeWalker graph becomes a controller action that runs it.
- Graph inputs become request parameters. Simple values (numbers, text, true/false, ids) come from the query string. On POST, PUT and PATCH, one object input comes from the JSON body.
- Column defaults work: a value like `'pending'` becomes the C# default and `now()` is left to the database. Optional columns are nullable, and array columns map to arrays.
- Functions without a graph get a placeholder that says to build one in the API Editor.

### Local Database
**Local Database** (desktop app) lets you try the project on your own Postgres before you ship it.

- **Test & save** stores the connection inside the encrypted project and creates the database if it doesn't exist.
- **Create from scratch** drops this project's schemas and runs SQL.sql and RLS.sql from the latest export. **Apply Migration.sql** upgrades instead.
- **Add sample data** fills every table with believable rows. Parent tables are filled first so foreign keys line up, unique columns stay unique, and `CHECK (column IN (...))` lists are respected. The rows are also saved as `SampleData.sql`.
- **Preview a query** shows results in a grid. INSERT, UPDATE and DELETE are rolled back unless you tick **Keep changes**.
- **Start API** runs the exported API (`dotnet run`, which needs the .NET SDK) at http://localhost:5180 and opens Swagger. **Stop** ends it.

### Row level security
Each policy has a **Command**, an **Access** level (own rows; public read with own writes; everyone; nobody; or custom SQL) and an **Owner column**, plus a line that says in plain words what the policy allows. Admins and moderators get the access they should, and regular users only see their own rows. The NodeWalker block **DB: RLS Set Role** is new, and **DB: RLS Set User** matches the generated policies.

---

## Look and feel

### Themes
The **Default** theme looks the way the app did before themes existed. If text or panels looked inverted after the theme update (light text on light panels, white NodeWalker header), that's fixed.

To make your own theme, create `DatabaseDesignerData\<your username>\Themes\<Theme Name>\theme.json`:

```json
{
  "Colors": {
    "Theme_BackgroundColor": "#0E0E12",
    "Theme_TextOnPrimaryColor": "#E6E6F0"
  },
  "BackgroundImage": "wallpaper.png"
}
```

- `Theme_BackgroundColor` is used for most text and dark surfaces, so keep it dark.
- `Theme_TextOnPrimaryColor` is the light color used for panels and for text on dark surfaces.
- `BackgroundImage` is optional; put the image in the same folder.

Y2K Chrome, Midnight and Sandstone are installed as starting points. Open **Themes** from the desktop: **Apply** switches right away, **Set Default** loads the theme on every launch.

### Music player
- The **My Music** tab plays songs from your Music folder (`.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`). They follow the volume slider, can be seeked, and keep playing when the player is closed.
- The player opens again properly while one of your own songs is playing (before, it wouldn't open at all).
- Each song shows how many times it has been played. A play counts when the song finishes, and again every time it repeats with Loop on. Play counts are saved per account.

### Wallpapers
Wallpapers (the desktop, the login screen and project wallpapers) fill the screen without stretching. On ultra-wide or very tall screens the edges are cropped instead.

### Desktop
- Double-clicking a desktop icon opens one window, not two.
- Closing a window removes its icon from the lower bar.
- The login greeting shows your username again, and the recovery code screen fits its window.

### Password recovery
New accounts get a recovery code when they sign up. Existing accounts get one the first time they sign in on this version; it's shown once, so save it. **Forgot Password? / Change Password** on the sign-in screen sets a new password using either the recovery code or your current password. Your projects are re-encrypted with the new password, and a backup is kept in `DatabaseDesignerData\<you>\Backups\`.

---

## Fixes in generated SQL
- Column lengths are applied (`varchar(50)`, `numeric(10,2)`); before, they were dropped.
- Default values containing an apostrophe work.
- Composite primary keys generate valid SQL.
- Double Precision and range columns (int4range, daterange and the rest) use type names Postgres understands.

## Guide
**GUIDE-Simple-Store.md** builds a store with inventory and Stripe payments step by step. The finished store is also the **Simple Store** project template.
