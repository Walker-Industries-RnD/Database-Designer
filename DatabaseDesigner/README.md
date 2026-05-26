# Database Designer

A powerful visual database design tool that lets you create, manage, and export PostgreSQL database schemas with integrated Row-Level Security (RLS) and API generation.

---

## Getting Started

### Installation

1. Download the latest release for your platform:
   - **Windows**: Run the `.exe` installer or portable `.zip`
   - **Web (OpenSilver)**: Host the `Database_Designer.Browser` folder on any web server
   - **Desktop (Photino)**: Use the native desktop build

2. Launch the application
3. Create an account or log in to start designing

### First Steps

1. **Create a Project**: Click "New Project" and give it a name
2. **Add Tables**: Use the "+" button or templates to create database tables
3. **Design Schema**: Add columns, set types, configure constraints
4. **Build & Export**: Generate SQL, C# models, and API code

---

## Core Features

### Table Designer

Create and manage database tables with a visual interface:

| Feature | Description |
|---------|-------------|
| **Columns** | Add rows with 20+ PostgreSQL data types |
| **Primary Keys** | Mark columns as primary keys |
| **Constraints** | NOT NULL, UNIQUE, CHECK expressions |
| **Defaults** | Static values or PostgreSQL functions |
| **Arrays** | Support for variable-length arrays |
| **Media/Encrypted** | Special column types for files and security |
| **Indexes** | B-tree, Hash, GIN, GiST with conditions |
| **References** | Foreign keys with ON DELETE/UPDATE actions |

**Adding a Column:**
1. Select a table in the sidebar
2. Click "Add Row" in the table editor
3. Set name, type, limits, and constraints
4. Click "Confirm" to save

### Template System

Jump-start your design with pre-built templates:

1. Click "Create New Table" from the main interface
2. Choose "Browse Templates" or "Blank Table"
3. Select a template category (e.g., Users, Posts, Forums)
4. Preview the schema and customize as needed
5. Click "Use Template" to add it to your project

**Template Categories:**
- User Management (profiles, sessions, roles)
- Content (posts, comments, media)
- Commerce (products, orders, payments)
- Social (friends, notifications, messages)
- Infrastructure (logs, queues, settings)

### Relationship Builder

Connect tables with foreign keys:

1. Open a table's "References" tab
2. Click "Add Reference"
3. Select the referenced table and column
4. Choose ON DELETE and ON UPDATE actions:
   - `CASCADE` - Delete/update child rows
   - `SET NULL` - Set foreign key to NULL
   - `SET DEFAULT` - Set to column default
   - `RESTRICT` - Prevent deletion
   - `NO ACTION` - No referential action

### Index Builder

Optimize query performance with indexes:

1. Open a table's "Indexes" tab
2. Click "Add Index"
3. Set index name and select columns
4. Choose index type:
   - **B-tree**: Default, good for equality/range
   - **Hash**: Fast equality lookups
   - **GIN**: Full-text search, JSON
   - **GiST**: Geometric, range types

5. Add WHERE conditions for partial indexes

---

## Row-Level Security (RLS)

RLS lets you control row-level access based on user roles.

### Managing Roles

1. Open the **RLS Editor** from the toolbar
2. Navigate to **Roles** tab (Page 1)
3. Add roles like "Standard Users", "Admins", "Moderators"
4. Assign icons and descriptions

### Configuring Table Access

1. Go to **Tables** tab (Page 2)
2. Add tables that this role should access
3. Set role description and permissions

### Creating Policies

1. Open **Policies** tab (Page 3)
2. Click "Add Policy"
3. Set policy name and category:
   - **Base Server**: Required for server operation
   - **Communication**: Messages, chat, servers
   - **Profiles**: Profile creation & management
   - **Economy**: Marketplaces & payments

4. Policies are generated as PostgreSQL RLS policies when you build

### RLS SQL Output

When you build your project, RLS.sql is generated with:
- Role definitions (CREATE ROLE)
- RLS enablement (ALTER TABLE ... ENABLE ROW LEVEL SECURITY)
- Policy creation (CREATE POLICY)
- Permission grants (GRANT SELECT/INSERT/UPDATE/DELETE)
- Helper functions (current_user_id, is_admin, is_moderator)

---

## API Generation

The API Editor lets you design RESTful API endpoints:

### Modules

1. Open the **API Editor** from the toolbar
2. Navigate to **Modules** tab (Page 1)
3. Create modules like "Users", "Posts", "Auth"
4. Assign icons and descriptions

### Endpoints

1. Go to **Endpoints** tab (Page 2)
2. Add endpoints: GET /users, POST /posts, etc.
3. Set HTTP methods and descriptions

### Functions

1. Open **Functions** tab (Page 3)
2. Define API functions:
   - Verb (GET, POST, PUT, DELETE, PATCH)
   - Name (the function/method name)
   - Description (what it does)
   - Tags (for grouping/organization)

### Generated API Output

Build generates a complete ASP.NET Core API:
```
GeneratedDB/v1/API/
├── Program.cs           # App configuration
├── appsettings.json     # Connection strings
├── API.csproj          # Project file
├── Controllers/        # API controllers
│   └── {Module}Controller.cs
└── Models/
    ├── AppDbContext.cs # EF Core context
    └── Models.cs       # Entity classes
```

---

## NodeWalker (Visual Scripting)

NodeWalker is a visual programming system for building complex database logic.

### Opening NodeWalker

1. Click the **NodeWalker** button in the toolbar
2. Create a new session or load an existing one
3. Right-click on the canvas to add nodes

### Node Categories

| Category | Nodes |
|----------|-------|
| **Logic** | Add, Subtract, Multiply, Divide, And, Or, Not, If, Compare |
| **String** | Concat, Format, Contains, Replace, ToLower, ToUpper, Trim |
| **Database** | db: get all, db: get where, db: add, db: update, db: remove |
| **Predicate** | where: equals, contains, and, or |
| **Math** | abs, min, max, round, power, sqrt |
| **Date** | now, add days, format |
| **Type** | String/Int/Float/Bool Literal, Cast, Custom Input |

### Lambda System

Create lambda expressions for LINQ queries:
1. Add a **Lambda** node
2. Set parameter as `LITERAL:x`
3. Connect expression to Body input
4. Use output with predicate nodes

### Saving Sessions

1. Click "Save" or Ctrl+S
2. Sessions are stored as JSON in your workspace

---

## Building Your Project

### Build Process

1. Click **Build** in the toolbar
2. Review validation errors (broken references, missing data)
3. Click **Build Project**

### Output Structure

```
{ProjectFolder}/
└── GeneratedDB/
    └── v1/
        ├── SQL.sql          # Table definitions, indexes, references
        ├── RLS.sql          # Row-Level Security policies
        ├── Documentation.md # Markdown documentation
        ├── Classes.cs       # C# model classes
        ├── Models.cs        # EF Core DbContext
        ├── API/             # ASP.NET Core Web API
        │   ├── Program.cs
        │   ├── appsettings.json
        │   ├── Controllers/
        │   └── Models/
        └── SpacetimeDB/     # SpacetimeDB module
            ├── SpacetimeDB.cs
            └── README.md
```

### Exporting Templates

1. Go to **Build** > **Export Template**
2. Fill in metadata (name, author, description)
3. Select tables to include
4. Click **Build Template**

---

## Database Composition

Connect to existing databases:

1. Click **Database Composition** from the toolbar
2. Enter connection string:
   ```
   Host=localhost;Database=mydb;Username=postgres;Password=secret
   ```
3. Click **Connect**
4. Browse existing tables and schemas
5. Import tables into your project

---

## Music Player

Customize your workspace ambiance:

1. Click the **Music** button in the lower toolbar
2. Choose from pre-loaded album songs or add custom tracks
3. Use the player controls:
   - Play/Pause
   - Next/Previous
   - Volume slider
   - Shuffle/Repeat

**Adding Custom Songs:**
1. Click "Add Custom Song"
2. Select an audio file (MP3, WAV, OGG)
3. Enter song title and artist
4. The file plays via JavaScript Audio API

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+S | Save |
| Ctrl+N | New Project |
| Ctrl+O | Open Project |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Delete | Delete selected |
| Ctrl+C | Copy |
| Ctrl+V | Paste |
| Ctrl+Scroll | Zoom canvas |

---

## Project Structure

```
DatabaseDesigner/
├── Database_Designer/       # Core application (OpenSilver/WPF)
│   ├── MainPage.xaml         # Main UI
│   ├── Build.xaml            # Build & export system
│   ├── RLSData.cs            # RLS data model
│   ├── RLS*Editor*.xaml      # RLS editor pages
│   ├── APIData.cs            # API data model
│   ├── API*Editor*.xaml      # API editor pages
│   ├── NodeWalker/           # Visual scripting
│   │   ├── NodeWalker.xaml  # Canvas & nodes
│   │   └── README.md        # NodeWalker docs
│   ├── DatabaseDesigner.dll  # Schema engine
│   └── Assets/              # Fonts, images, sounds
├── Database_Designer.Browser/ # Web deployment
├── Database_Designer.Photino/ # Native desktop
└── Database_Designer.Simulator/ # Testing
```

---

## Troubleshooting

### "Connection failed" error
- Check your PostgreSQL server is running
- Verify connection string credentials
- Ensure database accepts connections from your IP

### "Build failed" error
- Check the validation panel for broken references
- Ensure all foreign key targets exist
- Review error messages for specifics

### "RLS not working" error
- Verify RLS is enabled: `SELECT relrowsecurity FROM pg_class WHERE relname='tablename'`
- Check user role exists: `SELECT rolname FROM pg_roles`
- Test policy manually with `EXPLAIN` on affected queries

### "API returns 404"
- Ensure controller names match routes
- Check Program.cs for proper middleware
- Verify AppDbContext has all DbSets

---

## Support & Feedback

- **Issues**: Report bugs on the GitHub issues page
- **Features**: Suggest new features in discussions
- **Documentation**: Help improve this guide!

---

## License

See LICENSE file in repository root.