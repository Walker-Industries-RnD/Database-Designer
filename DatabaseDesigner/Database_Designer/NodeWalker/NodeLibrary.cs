using System;
using System.Collections.Generic;
using System.Linq;
using static Database_Designer.NodeWalker.NodeWalker.Node;
using NodeFlow = Database_Designer.NodeWalker.NodeWalker.Flow;

namespace Database_Designer.NodeWalker
{
    // Every built-in NodeWalker block, grouped by sidebar category. No UI
    // code here, so the compiler tests can use the real definitions.
    public static class NodeLibrary
    {
        public static List<Category> Create() => new()
        {
            new Category { Name = "Flow", Nodes = new() {
                CreateNode("Start",       "Entry point of the function. Wire its Then ▶ to the first step.", Array.Empty<Input>(), new[]{ new Output("Flow", typeof(object), "object") }),
                CreateNode("Branch", "Runs the nodes wired to True when Condition is true, otherwise the ones wired to False. After ▶ runs once either side is done.",
                    new[]{ new Input("Condition", typeof(object), "object", true) },
                    new[]{ ExecOut("True"), ExecOut("False") }),
                CreateNode("Sequence", "Runs Step 1, then Step 2, then Step 3, then Step 4. Later steps can use values from earlier ones.",
                    Array.Empty<Input>(),
                    new[]{ ExecOut("Step 1"), ExecOut("Step 2"), ExecOut("Step 3"), ExecOut("Step 4") }),
                CreateNode("For Each", "Runs Loop Body once for every item in Items. Item and Index only exist inside the loop body; After ▶ runs when the loop is done.",
                    new[]{ new Input("Items", typeof(object), "object", true) },
                    new[]{ ExecOut("Loop Body"), new Output("Item", typeof(object), "object"), new Output("Index", typeof(int), "int") }),
                CreateNode("Repeat", "Runs Loop Body Count times. Index goes 0, 1, 2…",
                    new[]{ new Input("Count", typeof(int), "int", true) },
                    new[]{ ExecOut("Loop Body"), new Output("Index", typeof(int), "int") }),
                CreateNode("Try", "Runs Try. If anything in it throws (including Fail If), runs Catch instead with the Error message. Leave Catch empty to ignore errors.",
                    Array.Empty<Input>(),
                    new[]{ ExecOut("Try"), ExecOut("Catch"), new Output("Error", typeof(string), "string") }),
                CreateNode("Return", "Stops here and returns the current outputs. Nothing after it on this path runs.",
                    Array.Empty<Input>(), Array.Empty<Output>()),
                CreateNode("End",         "Exit point of the function",  new[]{ new Input("Flow", typeof(object), "object", false) }, Array.Empty<Output>()),
                CreateNode("Event Input", "Graph input parameter",       Array.Empty<Input>(), new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Set Output",  "Graph output value",          new[]{ new Input("Value", typeof(object), "object", true) }, Array.Empty<Output>()),
                // Custom Input belongs with Flow - it adds a typed parameter to
                // the generated function. CUSTOMINPUT(MyType) marker in Logic.
                CreateNode("Custom Input", "Adds a typed parameter to the generated function. Set Value to CUSTOMINPUT(MyType) or CUSTOMINPUT(long orderId) to also name it.",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "custom") }),
            }},
            new Category { Name = "Variables", Nodes = new() {
                CreateNode("Get Variable", "Retrieves a named variable",
                    new[]{ new Input("Name", typeof(string), "string", true) },
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Set Variable", "Stores a value into a named variable",
                    new[]{ new Input("Name", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "Math", Nodes = new() {
                CreateNode("Add",      "A + B", Num2In(), Num1Out()),
                CreateNode("Subtract", "A - B", Num2In(), Num1Out()),
                CreateNode("Multiply", "A × B", Num2In(), Num1Out()),
                CreateNode("Divide",   "A ÷ B (throws on zero)", Num2In(), Num1Out()),
            }},
            new Category { Name = "Logic", Nodes = new() {
                CreateNode("If",  "Turns any value into True/False values (truthy/falsy). To choose which nodes run, use Flow > Branch.",
                    new[]{ new Input("Condition", typeof(object), "object", true) },
                    new[]{ new Output("True", typeof(object), "object"), new Output("False", typeof(object), "object") }),
                CreateNode("And", "A && B, accepts any value (truthy semantics)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Or",  "A || B, accepts any value (truthy semantics)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Not", "!Value, accepts any value (truthy semantics)",
                    new[]{ new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                // Sequencing primitive - wire any output into After to enforce ordering.
                CreateNode("Run After", "Force the next step to run after another node finishes: wire any output of that node into After, and the value you want to pass on into Value.",
                    new[]{ new Input("After", typeof(object), "object", true), new Input("Value", typeof(object), "object", false) },
                    new[]{ new Output("Then", typeof(object), "object") }),
                // Comparison operators - accept anything, compare via Equals/Comparer.
                CreateNode("Equals", "A == B (uses object.Equals)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Not Equals", "A != B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Less Than", "A < B (Comparer<object>.Default)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Greater Than", "A > B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Less Or Equal", "A <= B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Greater Or Equal", "A >= B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
            }},
            new Category { Name = "Lambda Logic", Nodes = new() {
                CreateNode("Select: Field", "Explicit key selector: x => x.<Property>. Wire into DB: Order By / Order By Desc instead of typing a lambda.",
                    new[]{ new Input("Property", typeof(string), "string", true) },
                    new[]{ new Output("Selector", typeof(object), "selector") }),
                CreateNode("Where: Equals", "Predicate: x.<Property> == <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Not Equals", "Predicate: x.<Property> != <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Greater", "Predicate: x.<Property> > <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Less", "Predicate: x.<Property> < <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Contains", "Predicate: x.<Property>.Contains(<Value>)",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Greater Or Equal", "Predicate: x.<Property> >= <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Less Or Equal", "Predicate: x.<Property> <= <Value>",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Starts With", "Predicate: x.<Property>.StartsWith(<Value>)",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Ends With", "Predicate: x.<Property>.EndsWith(<Value>)",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Value", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Like", "Case-insensitive pattern match (Postgres ILIKE, % = any text). Database queries only.",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Pattern", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Is Null", "Predicate: x.<Property> == null",
                    new[]{ new Input("Property", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Is Not Null", "Predicate: x.<Property> != null",
                    new[]{ new Input("Property", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Is True", "Predicate: x.<Property> == true (for bool columns)",
                    new[]{ new Input("Property", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Is False", "Predicate: x.<Property> == false (for bool columns)",
                    new[]{ new Input("Property", typeof(string), "string", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Between", "Predicate: Min <= x.<Property> <= Max (inclusive)",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Min", typeof(object), "object", true), new Input("Max", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: In", "Predicate: x.<Property> is one of Values (wire a List Literal). Becomes SQL IN (…).",
                    new[]{ new Input("Property", typeof(string), "string", true), new Input("Values", typeof(object), "object", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("DB: Query", "Filter + sort + page in one block. Every input except Db/EntityType is optional.",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Predicate",  typeof(object), "predicate", false),
                        new Input("OrderBy",    typeof(object), "selector",  false),
                        new Input("Descending", typeof(bool),   "bool",   false),
                        new Input("Skip",       typeof(int),    "int",    false),
                        new Input("Take",       typeof(int),    "int",    false)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Select Field", "Fetch just one column: .Where(Predicate).Select(Selector)",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Selector",   typeof(object), "selector",  true),
                        new Input("Predicate",  typeof(object), "predicate", false)
                    },
                    new[]{ new Output("Values", typeof(object), "object") }),
                CreateNode("DB: Delete Where", "Bulk delete every row matching Predicate (single SQL DELETE)",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Predicate",  typeof(object), "predicate", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("Where: And", "Combine two predicates with &&",
                    new[]{ new Input("A", typeof(object), "predicate", true), new Input("B", typeof(object), "predicate", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Or", "Combine two predicates with ||",
                    new[]{ new Input("A", typeof(object), "predicate", true), new Input("B", typeof(object), "predicate", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Not", "Negate a predicate with !",
                    new[]{ new Input("Predicate", typeof(object), "predicate", true) },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
            }},
            new Category { Name = "Store & API", Nodes = new() {
                CreateNode("Fail If", "Stop the request when Condition is true. The API answers 400 Bad Request with Message (e.g. \"Out of stock\").",
                    new[]{ new Input("Condition", typeof(object), "object", true), new Input("Message", typeof(string), "string", true) },
                    new[]{ new Output("Ok", typeof(bool), "bool") }),
                CreateNode("Object: Build", "Build a JSON object for a response: { Key1: Value1, … } (up to 6 pairs).",
                    Enumerable.Range(1, 6).SelectMany(k => new[] {
                        new Input($"Key{k}", typeof(string), "string", k == 1),
                        new Input($"Value{k}", typeof(object), "object", false) }).ToArray(),
                    new[]{ new Output("Object", typeof(object), "object") }),
                CreateNode("Env Var", "Read a server environment variable (API keys, secrets) so they never live in the graph.",
                    new[]{ new Input("Name", typeof(string), "string", true) },
                    new[]{ new Output("Value", typeof(string), "string") }),
                CreateNode("Set Field", "Set one property on an object/row (value is converted to the property's type).",
                    new[]{ new Input("Object", typeof(object), "object", true), new Input("Property", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    Array.Empty<Output>()),
                CreateNode("DB: Update Where", "Set Property = Value on every row matching Predicate, in one UPDATE statement.",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Predicate",  typeof(object), "predicate", true),
                        new Input("Property",   typeof(string), "string", true),
                        new Input("Value",      typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Increment Where", "Add Amount to Property on matching rows, atomically (e.g. restock).",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Predicate",  typeof(object), "predicate", true),
                        new Input("Property",   typeof(string), "string", true),
                        new Input("Amount",     typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Decrement Where", "Subtract Amount from Property on matching rows, atomically. Put \"stock >= amount\" in the predicate and 0 affected rows means out of stock.",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type),   "type",   true),
                        new Input("Predicate",  typeof(object), "predicate", true),
                        new Input("Property",   typeof(string), "string", true),
                        new Input("Amount",     typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("HTTP: Post Form", "POST a form-encoded body (a=1&b=2), which is what Stripe and many payment APIs expect.",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Form",   typeof(string), "string", true)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Read JSON Field", "Read one field from a JSON response by path, e.g. \"client_secret\" or \"data.0.id\".",
                    new[]{
                        new Input("Response", typeof(object), "custom", true, "HttpResponseMessage"),
                        new Input("Path",     typeof(string), "string", true)
                    },
                    new[]{ new Output("Value", typeof(string), "string") }),
            }},
            new Category { Name = "List Logic", Nodes = new() {
                CreateNode("List: Filter", "Keep items matching a predicate (wire any Where: block)",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Predicate", typeof(object), "predicate", true) },
                    new[]{ new Output("Items", typeof(object), "object") }),
                CreateNode("List: First Where", "First item matching a predicate, or null",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Predicate", typeof(object), "predicate", true) },
                    new[]{ new Output("Item", typeof(object), "object") }),
                CreateNode("List: Any Where", "True if any item matches the predicate",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Predicate", typeof(object), "predicate", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("List: Count Where", "How many items match the predicate",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Predicate", typeof(object), "predicate", true) },
                    new[]{ new Output("Count", typeof(int), "int") }),
                CreateNode("List: Sort By", "Sort by a field (wire Select: Field)",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Selector", typeof(object), "selector", true), new Input("Descending", typeof(bool), "bool", false) },
                    new[]{ new Output("Items", typeof(object), "object") }),
                CreateNode("List: Map Field", "Pull one field out of every item (wire Select: Field)",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Selector", typeof(object), "selector", true) },
                    new[]{ new Output("Values", typeof(object), "object") }),
                CreateNode("List: Sum Field", "Total of a numeric field, e.g. order line prices (wire Select: Field)",
                    new[]{ new Input("Items", typeof(object), "object", true), new Input("Selector", typeof(object), "selector", true) },
                    new[]{ new Output("Total", typeof(decimal), "decimal") }),
            }},
            new Category { Name = "String", Nodes = new() {
                CreateNode("Concat", "Joins two strings",
                    new[]{ new Input("A", typeof(string), "string", true), new Input("B", typeof(string), "string", true) },
                    new[]{ new Output("Result", typeof(string), "string") }),
                CreateNode("Format", "Fill {0} {1} {2} in a template (culture-invariant, so 12.5 stays 12.5). {0:0} = no decimals.",
                    new[]{ new Input("Template", typeof(string), "string", true), new Input("Arg0", typeof(object), "object", false),
                           new Input("Arg1", typeof(object), "object", false), new Input("Arg2", typeof(object), "object", false) },
                    new[]{ new Output("Result", typeof(string), "string") }),
            }},
            new Category { Name = "Literals", Nodes = new() {
                CreateNode("String Literal", "A constant string value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(string), "string") }),
                CreateNode("Int Literal", "A constant integer value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(int), "int") }),
                CreateNode("Float Literal", "A constant floating-point value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(double), "number") }),
                CreateNode("Bool Literal", "A constant boolean value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(bool), "bool") }),
                CreateNode("Null", "A null reference",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("JSON Literal", "Multi-line JSON constant (parsed at runtime)",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Connection String Literal", "A Postgres connection string",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(string), "string") }),
                CreateNode("Predicate Literal", "A LINQ predicate, e.g. \"x => x.IsActive\"",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Custom Literal", "A custom-typed constant. Inspector field; first line is the type, rest is JSON or string.",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "custom") }),
                CreateNode("List Literal", "A constant list: comma or newline separated. All whole numbers make long[], otherwise string[].",
                    Array.Empty<Input>(),
                    new[]{ new Output("Values", typeof(object), "object") }),
                CreateNode("Type Literal", "A constant Type reference (e.g., typeof(Game))",
                     Array.Empty<Input>(),
                    new[]{ new Output("Type", typeof(Type), "type") }),
             }},
            new Category { Name = "Objects", Nodes = new() {
                CreateNode("Expose", "Pull a single property out of an object. Inspector field = property name.",
                    new[]{ new Input("Object", typeof(object), "object", true) },
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Cast", "Cast a value to a target type. Inspector field = type name (e.g. \"User\").",
                    new[]{ new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(object), "custom") }),
                CreateNode("WebURL", "Wraps a URL string as Uri",
                    new[]{ new Input("URL", typeof(string), "string", true) },
                    new[]{ new Output("URI", typeof(object), "weburl") }),
            }},
            new Category { Name = "HTTP", Nodes = new() {
                // All HTTP nodes default to HTTP/2 with a fallback to HTTP/1.1.
                CreateNode("HTTP: New Client",
                    "Create an HttpClient configured for HTTP/2 (HttpVersion=2.0, VersionPolicy=RequestVersionOrLower).",
                    Array.Empty<Input>(),
                    new[]{ new Output("Client", typeof(object), "custom", "HttpClient") }),
                CreateNode("HTTP: Get", "GET <url>; returns the HttpResponseMessage",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Post JSON", "POST a serialised body to <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Body",   typeof(object), "object", false)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Put JSON", "PUT a serialised body to <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Body",   typeof(object), "object", false)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Delete", "DELETE <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: New Request", "Build an HttpRequestMessage for HTTP: Send (Method e.g. GET/POST, optional JSON Body)",
                    new[]{
                        new Input("Method", typeof(string), "string", true),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Body",   typeof(object), "object", false)
                    },
                    new[]{ new Output("Request", typeof(object), "custom", "HttpRequestMessage") }),
                CreateNode("HTTP: Send", "Send any HttpRequestMessage (full control)",
                    new[]{
                        new Input("Client",  typeof(object), "custom", true, "HttpClient"),
                        new Input("Request", typeof(object), "custom", true, "HttpRequestMessage")
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Read JSON", "Deserialise the response body; Inspector field = target type",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Value", typeof(object), "custom") }),
                CreateNode("HTTP: Read String", "Read the response body as a string",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Body", typeof(string), "string") }),
                CreateNode("HTTP: Status Code", "HTTP status code as an int",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Code", typeof(int), "int") }),
                CreateNode("HTTP: Set Bearer Token", "Authorization: Bearer <token>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Token",  typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("HTTP: Set Header", "Add a default request header",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Name",   typeof(string), "string", true),
                        new Input("Value",  typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("HTTP: Ensure Success", "Throw if the response was not 2xx",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "EF Core: Easy", Nodes = new() {
                // Step-by-step beginner nodes that read like English.
                CreateNode("DB: Open", "Open the project's database (Postgres connection string in)",
                    new[]{ new Input("ConnectionString", typeof(string), "string", true) },
                    new[]{ new Output("Db", typeof(object), "custom", "AppDbContext") }),
                CreateNode("DB: Save", "Save all pending changes to disk",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Close", "Close + dispose the database",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    Array.Empty<Output>()),
                CreateNode("DB: Get All", "Get every row of one table",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                    new Input("EntityType", typeof(Type), "type", true), 
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Get One By Id", "Get a single row by its primary key",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                    new Input("EntityType", typeof(Type), "type", true),  
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("DB: Get Where", "Get rows matching a predicate (e.g. \"x => x.IsActive\")",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Get First", "First match or null",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("DB: Count", "How many rows match",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", false)
                    },
                    new[]{ new Output("Count", typeof(int), "int") }),
                // Predicate builder nodes live in the "Lambda Logic" category.

                CreateNode("DB: Add", "Stage an entity for insert (call DB: Save afterward)",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("DB: Add And Save", "Insert immediately. Saved = the same row with its generated id.",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int"), new Output("Saved", typeof(object), "object") }),
                CreateNode("DB: Update And Save", "Persist changes on a tracked entity",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Remove And Save", "Delete a row immediately",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Exists", "Does any row match?",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Exists", typeof(bool), "bool") }),
                CreateNode("DB: Begin Tx", "Start a database transaction",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    new[]{ new Output("Tx", typeof(object), "custom", "IDbContextTransaction") }),
                CreateNode("DB: Commit Tx", "Commit a transaction",
                    new[]{ new Input("Tx", typeof(object), "custom", true, "IDbContextTransaction") },
                    Array.Empty<Output>()),
                CreateNode("DB: Rollback Tx", "Rollback a transaction",
                    new[]{ new Input("Tx", typeof(object), "custom", true, "IDbContextTransaction") },
                    Array.Empty<Output>()),
                // Result-shaping helpers
                CreateNode("DB: Order By", "Order rows ascending by a key (\"x => x.CreatedAt\")",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("KeySelector",typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Order By Desc", "Order rows descending by a key",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("KeySelector",typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Page", "Skip + Take pagination on a table",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Skip",       typeof(int),    "int",    true),
                        new Input("Take",       typeof(int),    "int",    true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Include", "Eager-load a navigation property",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Navigation", typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                // Row-Level Security primitives
                CreateNode("DB: RLS Enable", "ALTER TABLE x ENABLE ROW LEVEL SECURITY",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Disable", "ALTER TABLE x DISABLE ROW LEVEL SECURITY",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Force", "Force RLS even for the table owner",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Create Policy",
                    "CREATE POLICY <name> ON <table> FOR <op> TO <role> USING (<using>) WITH CHECK (<check>)",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("PolicyName", typeof(string), "string", true),
                        new Input("Operation",  typeof(string), "string", false),    // ALL / SELECT / INSERT / UPDATE / DELETE
                        new Input("Role",       typeof(string), "string", false),
                        new Input("Using",      typeof(string), "string", true),
                        new Input("WithCheck",  typeof(string), "string", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Drop Policy", "DROP POLICY IF EXISTS <name> ON <table>",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("PolicyName", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Set User",
                    "Tell the database who is calling: sets app.current_user_id, which RLS.sql policies read via current_user_id()",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("UserId", typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("DB: RLS Set Role", "Act as one of your RLS roles (e.g. standard_users) so its policies apply to the following queries",
                    new[]{
                        new Input("Db",   typeof(object), "custom", true, "AppDbContext"),
                        new Input("Role", typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("DB: RLS Reset User", "Clear the current user id / role (back to the connection's own role)",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    Array.Empty<Output>()),
                CreateNode("DB: Raw SQL", "Execute a raw SQL command; escape hatch for anything",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Sql",    typeof(string), "string", true),
                        new Input("Params", typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
            }},
            new Category { Name = "Postgres: Easy", Nodes = new() {
                CreateNode("PG: Connect", "Open a Postgres connection (Npgsql)",
                    new[]{ new Input("ConnectionString", typeof(string), "string", true) },
                    new[]{ new Output("Connection", typeof(object), "custom", "NpgsqlConnection") }),
                CreateNode("PG: Query", "SELECT; returns rows (Dapper)",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("PG: Query First", "First row only; null if none",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("PG: Execute", "INSERT/UPDATE/DELETE/DDL; returns rows affected",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Insert", "INSERT INTO <table> VALUES (@…) RETURNING id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Entity",     typeof(object), "custom", true)
                    },
                    new[]{ new Output("Id", typeof(object), "object") }),
                CreateNode("PG: Update By Id", "UPDATE <table> SET … WHERE id = @id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Entity",     typeof(object), "custom", true),
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Delete By Id", "DELETE FROM <table> WHERE id = @id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Close", "Close + dispose a connection",
                    new[]{ new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection") },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "Postgres: Advanced", Nodes = new() {
                CreateNode("PG: Bulk Insert", "Streaming COPY; fastest for large inserts. Rows = IEnumerable<T>",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Columns",    typeof(object), "object", true), // string[]
                        new Input("Rows",       typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(long), "long") }),
                CreateNode("PG: Begin Tx", "Open a transaction",
                    new[]{ new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection") },
                    new[]{ new Output("Transaction", typeof(object), "custom", "NpgsqlTransaction") }),
                CreateNode("PG: Commit Tx", "Commit a transaction",
                    new[]{ new Input("Transaction", typeof(object), "custom", true, "NpgsqlTransaction") },
                    Array.Empty<Output>()),
                CreateNode("PG: Rollback Tx", "Rollback a transaction",
                    new[]{ new Input("Transaction", typeof(object), "custom", true, "NpgsqlTransaction") },
                    Array.Empty<Output>()),
                CreateNode("PG: Prepare", "Pre-compile a SQL statement; reuse via PG: Run Prepared",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true)
                    },
                    new[]{ new Output("Command", typeof(object), "custom", "NpgsqlCommand") }),
                CreateNode("PG: Run Prepared", "Execute a prepared command with named params",
                    new[]{
                        new Input("Command", typeof(object), "custom", true, "NpgsqlCommand"),
                        new Input("Params",  typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Batch Execute", "Send a batch of statements in one round-trip (NpgsqlBatch)",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Statements", typeof(object), "object", true) // string[]
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Notify", "NOTIFY <channel>, <payload>",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Channel",    typeof(string), "string", true),
                        new Input("Payload",    typeof(string), "string", false)
                    },
                    Array.Empty<Output>()),
                CreateNode("PG: Listen", "LISTEN <channel> + register a callback",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Channel",    typeof(string), "string", true),
                        new Input("Callback",   typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "SpacetimeDB: Easy", Nodes = new() {
                CreateNode("SDB: Connect", "Open a SpacetimeDB client connection",
                    new[]{
                        new Input("Uri",      typeof(string), "string", true),
                        new Input("Module",   typeof(string), "string", true),
                        new Input("AuthToken",typeof(string), "string", false)
                    },
                    new[]{ new Output("Conn", typeof(object), "custom", "DbConnection") }),
                CreateNode("SDB: Disconnect", "Close the client connection",
                    new[]{ new Input("Conn", typeof(object), "custom", true, "DbConnection") },
                    Array.Empty<Output>()),
                CreateNode("SDB: Subscribe", "Subscribe to one or more SQL queries",
                    new[]{
                        new Input("Conn",    typeof(object), "custom", true, "DbConnection"),
                        new Input("Queries", typeof(object), "object", true) // string[]
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: Call Reducer", "Invoke a server-side reducer by name with positional args",
                    new[]{
                        new Input("Conn",    typeof(object), "custom", true, "DbConnection"),
                        new Input("Reducer", typeof(string), "string", true),
                        new Input("Args",    typeof(object), "object", false)  // params object[]
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: Iter Table", "Iterate a synced table",
                    new[]{
                        new Input("Conn",  typeof(object), "custom", true, "DbConnection"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("SDB: Find By Pk", "Find a row by primary key on a synced table",
                    new[]{
                        new Input("Conn",  typeof(object), "custom", true, "DbConnection"),
                        new Input("Table", typeof(string), "string", true),
                        new Input("Pk",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("SDB: On Insert", "Register an insert callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: On Update", "Register an update callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: On Delete", "Register a delete callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
            }}
        };

        private static Input[] Num2In() => new[] { new Input("A", typeof(double), "number", true), new Input("B", typeof(double), "number", true) };
        private static Output[] Num1Out()    => new[] { new Output("Result", typeof(double), "number") };
        private static Input[] Bool2In() => new[] { new Input("A", typeof(bool), "bool", true), new Input("B", typeof(bool), "bool", true) };
        private static Output[] Bool1Out()    => new[] { new Output("Result", typeof(bool), "bool") };

        private static Output ExecOut(string name) => new(name, typeof(object), NodeFlow.ExecSemantic);

        private static BareNode CreateNode(string title, string desc,
            IEnumerable<Input> inputs, IEnumerable<Output> outputs, string logic = null) => new()
        {
            Title = title, Description = desc,
            Inputs = inputs.ToHashSet(), Outputs = outputs.ToHashSet(),
            UUID = Guid.NewGuid().ToString(),
            Logic = logic ?? $"// {title}",
            SyncType = "Sync"
        };
    }

    public class Category
    {
        public string Name { get; set; }
        public List<BareNode> Nodes { get; set; } = new();
    }
}
