# NodeWalker Documentation

## Overview

NodeWalker is a visual scripting system for Database Designer that lets you build C# scripts using a node-based editor. Instead of writing code directly, you connect nodes together to create logic flows that compile into executable C#.

---

## Core Concepts

### Nodes
Nodes are building blocks that perform actions or provide data. Each node has:
- **Title**: What the node does
- **Inputs** (left side): Data the node needs to work
- **Outputs** (right side): Results the node produces
- **Logic**: Optional inline C# code or configuration

### Connections
Wires connect an output port (right side) to an input port (left side). Data flows from left to right through connections.

### Sessions
A Session is your complete script - all nodes, connections, positions, and settings. Sessions are saved as `.json` files.

---

## Getting Started

### Opening NodeWalker
1. Open Database Designer
2. Navigate to the NodeWalker section
3. Click "New Session" or "Load Session"

### Adding Nodes
1. Right-click on the canvas to open the context menu
2. Select "Add Node" to open the node library
3. Browse or search for nodes by category
4. Double-click or click "Add" to place the node

### Connecting Nodes
1. Click and drag from an output port (right side)
2. Release on an input port (left side)
3. A wire connects the two ports

### Deleting Connections
- Right-click on a wire and select "Delete"
- Or drag from a connected input port to disconnect

---

## Node Categories

### Logic Nodes
Basic programming constructs:
- **Add, Subtract, Multiply, Divide** - Math operations
- **And, Or, Not** - Boolean logic
- **If** - Conditional branching
- **Equals, Not Equals, Less Than, Greater Than** - Comparisons
- **Lambda** - Create anonymous functions

### String Nodes
Text manipulation:
- **Concat** - Combine two strings
- **Format** - String.Format template
- **String Literal** - Fixed text value
- **logic: string contains/starts with/ends with** - Search operations
- **logic: string to lower/upper/trim** - Transform

### Variable Nodes
- **Get Variable** - Read from variables dictionary
- **Set Variable** - Write to variables dictionary

### Database Nodes (EF Core)
Entity Framework Core operations:
- **db: get all** - Fetch all entities
- **db: get where** - Filter with predicate
- **db: get one by id** - Find by primary key
- **db: add** - Add new entity
- **db: update and save** - Modify and persist
- **db: remove and save** - Delete entity

### Predicate Builder Nodes
Build lambda expressions for queries:
- **where: equals** - Property == value
- **where: not equals** - Property != value
- **where: greater/less** - Numeric comparisons
- **where: contains** - String contains
- **where: and/or** - Combine predicates

### Math Nodes
- **logic: math: abs/min/max/round/floor/ceiling** - Common operations
- **logic: math: power/sqrt** - Advanced math

### Date/Time Nodes
- **logic: date: now/utc now/today** - Get current time
- **logic: date: add days/hours/minutes** - Time arithmetic
- **logic: date: format/parse** - Convert formats

### Type Nodes
- **String Literal / Int Literal / Float Literal / Bool Literal** - Fixed values
- **Custom Literal** - Any type with custom body
- **Custom Input** - Function parameter
- **Cast** - Type conversion
- **Expose** - Property access via reflection

---

## Lambda System

### How Lambdas Work
Lambda expressions are used in LINQ queries (Where, Select, OrderBy, etc.). The Lambda node creates these expressions.

### Using the Lambda Node
1. Place a **Lambda** node
2. Set the parameter name in the Logic field as `LITERAL:parameterName` (e.g., `LITERAL:x`)
3. Connect an expression to the **Body** input
4. The output is a lambda like `x => <body expression>`

### Example: Filter Users by Name
```
[User Names: string[]] → [Lambda: Body]
                            ↓
                      x => x.Contains("John")
                            ↓
                      [db: get where Predicate]
```

### Predicate Nodes
Predicate builder nodes create lambda expressions for database queries:
1. Connect entity type
2. Wire comparison values
3. Output goes to `db: get where` predicate input

---

## Compiling Scripts

### Exporting to C#
1. Open your session with nodes connected
2. Click "Compile" or "Export Script"
3. The system generates C# code from your node graph
4. Copy or save the generated code

### Generated Script Structure
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Your generated class
public static class SessionNameScript
{
    public static async Task<ResultType> DoTheThing(Parameters)
    {
        // Node execution code
    }
}
```

---

## Saving and Loading

### Save Session
1. Click "Save" or Ctrl+S
2. Enter a session name
3. Session saves to: `{workspace}/NodeWalker/{sessionName}.json`

### Load Session
1. Click "Load" or use the session list
2. Select a saved session
3. Nodes, connections, and positions restore

---

## Tips and Tricks

### Making Scripts Async
Set `SyncType = "Async"` on nodes that need async operations, or enable async mode in session settings.

### Required vs Optional Ports
- **Yellow dot** = Required port (must be connected)
- **Red dot** = Optional port
- **Purple dot** = Custom type

### Default Values
Unconnected optional ports use default values (0, empty string, false, etc.).

### Variable Scope
Variables created with `Set Variable` are available to all subsequent nodes in topological order.

---

## Troubleshooting

### "Required input not connected" warning
Connect all yellow-dot input ports or provide a literal value.

### Cycle detected error
Your node graph has circular dependencies. Reorder connections to create a valid flow.

### Compile fails
Check warnings in the generated code. Missing connections use defaults which may cause type errors.

---

## Node Library Reference

### Complete Node List
| Category | Nodes |
|----------|-------|
| Math | add, subtract, multiply, divide |
| Logic | and, or, not, if, equals, not equals, less than, greater than |
| String | concat, format, string literal |
| Variables | get variable, set variable |
| Database | db: get all, db: get where, db: add, db: update and save, db: remove and save |
| Predicates | where: equals, where: contains, where: and, where: or |
| Math | logic: math: abs, min, max, round, floor, ceiling, power, sqrt |
| String | logic: string contains, starts with, ends with, replace, to lower, to upper, trim |
| Date | logic: date: now, add days, format, parse |
| Type | cast, expose, custom literal, custom input |
| Lambda | lambda, predicate literal |

---

## File Locations

- **Sessions**: `{workspace}/NodeWalker/*.json`
- **Exported Scripts**: `{workspace}/NodeWalker/Export/`
- **Settings**: Stored in session JSON files