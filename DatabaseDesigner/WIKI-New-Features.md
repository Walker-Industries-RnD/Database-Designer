# Database Designer — New Features

This page documents the features added in the latest update. Copy/adapt into the public wiki as needed.

---

## Portable Project Export & Import

Projects are normally saved encrypted with your account, so they can't be moved between machines or accounts. **Portable export** writes an unencrypted, shareable copy that anyone can import.

### Export a project
1. Open the **Projects** window.
2. On the project's card, click **Export (Portable)**.
3. A folder is written to:
   `DatabaseDesignerData\<your username>\Exports\<Project Name>\`
   It contains `session.json`, the project's `Scripts\` (your NodeWalker functions **and** their visual layout), and the project icon.

### Import a project
1. Copy an exported project folder into:
   `DatabaseDesignerData\<your username>\Imports\`
2. Open the **Projects** window and click **Import Project**.
3. Every folder in `Imports\` that contains a `session.json` is imported as a new project (re-encrypted for your account). Duplicate names get a numbered suffix.

> Functions and their visual node graphs travel with the project — no need to rebuild them after importing.

---

## Templates now include functions & visuals

When you build a **project template** or **row template**, the project's `Scripts\` folder (NodeWalker functions + their on-canvas layout) is now bundled into the template and restored automatically when someone creates a project from it. Templates are stored **unencrypted**.

### ⚠️ Delete old-version templates
Templates built before this update do **not** contain the new bundled data and may not import cleanly. Delete the old ones from:
- `DatabaseDesignerData\<your username>\Row Templates\`
- `DatabaseDesignerData\<your username>\Project Templates\`

Then re-export any templates you want to keep so they pick up the new format.

---

## Built-in example templates (installed by default)

Example templates are now **installed automatically** the first time you log in (existing users get them on next launch too). Nothing to copy.

**Row templates** — appear in **Create New Table → Template Pack**:

| Template | Table | Use |
|---|---|---|
| **User Accounts** | `users` | Secure sign-in: unique username/email, hashed password, `created_at` |
| **Blog Posts** | `posts` | Title, body, published flag, timestamp |
| **Inventory Items** | `items` | SKU, name, quantity, price, JSONB metadata |

**Project templates** — appear in the project template picker:

| Template | Tables | Use |
|---|---|---|
| **Secure Sign-In** | `users`, `sessions` | Auth starter with session references |
| **Simple Chat** | `users`, `messages` | Messages reference their sender |
| **Simple Marketplace** | `users`, `items`, `orders` | Sellers list items; buyers place orders |

They install into `DatabaseDesignerData\<your username>\Row Templates\` and `\Project Templates\`. To add your own, drop a pack folder there in the form `...\Row Templates\<Pack>\v1\Template.DsgnRowTmplate`. The same packs also live in the repo under `ExampleTemplates\` for reference.

---

## Music Player

### Custom songs now play
Drop audio files (`.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`) into your OS **Music** folder. They appear under **Custom Songs** in the player and now play through the desktop audio engine (NAudio). Custom songs:
- Respond to the **volume** slider,
- Keep playing after you close the player window (like the built-in tracks),
- Support the progress bar (seek by dragging).

The music player can now be **reopened** after closing (a bug where it stayed "already open" is fixed).

### Play counter
Each song shows a **▶ N plays** count under its title. The count increases every time you start the song and is saved per account, between sessions, in `DatabaseDesignerData\<your username>\playcounts.json`.

### Playlists
Open the **Playlists** tab in the song list:
1. Type a name and press **Create**.
2. Click a playlist to make it **active** (it highlights).
3. Press the **+** button on any song row to add it to the active playlist.
4. Press **▶** on a playlist to play it start-to-finish (auto-advances); **🗑** deletes it.

Playlists are saved per account in `DatabaseDesignerData\<your username>\playlists.json`.

---

## Themes

You can re-skin the app with your own colors, background, fonts, and images.

### Create a theme
Make a folder in `DatabaseDesignerData\<your username>\Themes\<Theme Name>\` containing a **`theme.json`**:

```json
{
  "Colors": {
    "Theme_BackgroundColor": "#0E0E12",
    "Theme_TextOnPrimaryColor": "#E6E6F0"
  },
  "BackgroundImage": "wallpaper.png"
}
```

- **Colors** — any theme resource key mapped to a `#RRGGBB` or `#AARRGGBB` hex value.
- **BackgroundImage** — optional file name (place the image in the same folder) used for the desktop wallpaper.

Two starter themes (**Midnight**, **Sandstone**) are created automatically the first time you log in — copy one as a starting point.

### Apply / set default
Open the **Themes** app from the launcher:
- **Apply** — switches to the theme immediately.
- **Set Default** — the chosen theme loads automatically on every launch.

---

## NodeWalker: Lambda Logic (explicit blocks, no hand-written lambdas)

The freeform **Lambda** node has been replaced by a dedicated **Lambda Logic** category with explicit blocks, so you build API/query logic visually instead of typing `x => …`:

- **Select: Field** — an explicit key selector (`x => x.<Property>`). Wire it into **DB: Order By** / **Order By Desc** instead of typing a lambda.
- **Where: Equals / Not Equals / Greater / Less / Contains** — comparison predicates.
- **Where: And / Or / Not** — combine predicates with correct precedence and no dangling parameters.

Wire any predicate into **DB: Get First / Get Where / Count** as before.

---

## Building the backend: what gets exported

When you **Build/Export** a project you get a runnable backend. Each part now carries real logic:

- **`SQL.sql`** — Postgres schema (tables, types).
- **`RLS.sql`** — full row-level-security: `CREATE ROLE`, `ENABLE/FORCE ROW LEVEL SECURITY`, `CREATE POLICY … USING (…) WITH CHECK (…)`, grants, role hierarchy, and helper functions like `current_user_id()`.
- **`Models/` + `Program.cs` + `API.csproj`** — an ASP.NET Core API project with EF Core `AppDbContext` and Swagger.
- **`Controllers/`** — one controller per API module.

### API endpoints now ship your NodeWalker logic
Previously every generated endpoint was a `// TODO` stub. Now, for each API function that has a NodeWalker graph:

1. The graph is compiled to C# and written to **`Controllers/Logic/<slug>.g.cs`**.
2. The controller action **calls that compiled logic** and returns its result:

```csharp
[HttpGet("getactiveusers")]
public async Task<IActionResult> GetActiveUsers()
{
    // Logic generated from NodeWalker graph -> Controllers/Logic/API_users_list_getactiveusers.g.cs
    var result = await GetActiveUsersScript.DoACoolThing();
    return Ok(result);
}
```

- Functions **without** a graph still emit a clear stub telling you to build one in NodeWalker.
- Graphs that need **input parameters** (Event Input / Custom Input nodes) generate the logic file and a note showing the call to wire up, since those inputs come from the HTTP request.

---

## RLS & API editors

Both the **RLS Editor** and **API Editor** now have an **X** button (top-right) to close them. Closing an editor now fully removes its window (previously it was only hidden, which could stack copies); reopening loads your last saved state.
