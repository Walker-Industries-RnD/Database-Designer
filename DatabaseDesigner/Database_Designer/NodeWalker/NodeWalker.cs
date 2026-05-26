using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Database_Designer.NodeWalker.NodeWalker.Node;

// NOTE: Pariah_Cybersecurity imports removed from core logic.
// Save/Load now uses System.Text.Json directly for reliability.
// If you need encryption, wrap the JSON string before writing to disk.

namespace Database_Designer.NodeWalker
{
    public static class NodeWalker
    {
        // Node types

        public static class Node
        {
            public class BareNode
            {
                public string Title { get; set; }
                public string Description { get; set; }
                public string RelativeIconPath { get; set; }
                public HashSet<Input> Inputs { get; set; } = new();
                public HashSet<Output> Outputs { get; set; } = new();
                public string Logic { get; set; }
                public string UUID { get; set; }
                public string SyncType { get; set; } = "Sync"; // "Sync" | "Async"
                public bool IsAsync => SyncType == "Async";

                public BareNode() { }

                public BareNode(string title, string description, string iconPath,
                    HashSet<Input> inputs, HashSet<Output> outputs,
                    string logic, string uuid, string syncType)
                {
                    Title = title;
                    Description = description;
                    RelativeIconPath = iconPath;
                    Inputs = inputs;
                    Outputs = outputs;
                    Logic = logic;
                    UUID = uuid;
                    SyncType = syncType;
                }

                /// <summary>Returns all required inputs that have no incoming connection in the session.</summary>
                public List<Input> GetUnconnectedRequiredInputs(SessionData.Session session)
                {
                    var connected = session.Connections
                        .Where(c => c.Node2UUID == UUID)
                        .Select(c => c.Node2Port)
                        .ToHashSet();

                    return Inputs
                        .Where(i => i.Required && !connected.Contains(i.Name))
                        .ToList();
                }
            }

            public class Input
            {
                public string Name { get; set; }
                [JsonIgnore] public Type Type { get; set; }
                public string TypeName { get; set; }   // for JSON serialization
                public string SemanticType { get; set; }
                public bool Required { get; set; }
                public string CustomTypeName { get; set; } // filled when SemanticType == "custom"

                public Input() { }

                public Input(string name, Type type, string semanticType, bool required, string customTypeName = null)
                {
                    Name = name;
                    Type = type;
                    TypeName = type?.FullName;
                    SemanticType = semanticType;
                    Required = required;
                    CustomTypeName = customTypeName;
                }

                public override bool Equals(object obj)
                {
                    if (obj is not Input other) return false;
                    return Name == other.Name && SemanticType == other.SemanticType && Required == other.Required;
                }
                public override int GetHashCode() => HashCode.Combine(Name, SemanticType, Required);
            }

            public class Output
            {
                public string Name { get; set; }
                public string SemanticType { get; set; }
                [JsonIgnore] public Type Type { get; set; }
                public string TypeName { get; set; }   // for JSON serialization
                public string CustomTypeName { get; set; }

                public Output() { }

                public Output(string name, Type type, string semanticType, string customTypeName = null)
                {
                    Name = name;
                    Type = type;
                    TypeName = type?.FullName;
                    SemanticType = semanticType;
                    CustomTypeName = customTypeName;
                }

                public override bool Equals(object obj)
                {
                    if (obj is not Output other) return false;
                    return Name == other.Name && SemanticType == other.SemanticType;
                }
                public override int GetHashCode() => HashCode.Combine(Name, SemanticType);
            }

            public class Connection
            {
                public string Node1UUID { get; set; }
                public string Node2UUID { get; set; }
                public string Node1Port { get; set; }
                public string Node2Port { get; set; }

                public Connection() { }

                public Connection(string node1, string node2, string port1, string port2)
                {
                    Node1UUID = node1;
                    Node2UUID = node2;
                    Node1Port = port1;
                    Node2Port = port2;
                }
            }
        }

        // Session data

        public static class SessionData
        {
            public class Session
            {
                public string Name { get; set; }
                public string Description { get; set; }
                public HashSet<Node.BareNode> Nodes { get; set; } = new();
                public Dictionary<string, Vector3> NodePositions { get; set; } = new();
                public HashSet<Node.Connection> Connections { get; set; } = new();
                public List<Chunk> Chunk { get; set; } = new();
                public List<Note> Notes { get; set; } = new();
                public List<Node.BareNode> CustomScripts { get; set; } = new();
                public string FunctionName { get; set; } = "DoACoolThing";
                public bool IsAsync { get; set; }

                // Project-level using directives the user wants to inject at
                // the top of the generated file. Stored as bare namespaces
                // ("System.Linq"), no leading "using" / trailing ";". Merged
                // with the compiler's defaults and any "using …;" lines
                // hoisted out of custom-node Logic before being emitted.
                public List<string> Usings { get; set; } = new();

                // Persisted viewport state. Following the pattern used by
                // Godot's GraphEdit, n8n, and ComfyUI: store the user's pan
                // offset (and reserve room for zoom) so reloading a session
                // restores the exact view. HasViewport disambiguates a saved
                // (0,0) viewport from a legacy file with no viewport state at
                // all — old files take the fit-to-content code path instead.
                public bool HasViewport { get; set; }
                public double ViewOffsetX { get; set; }
                public double ViewOffsetY { get; set; }
                public double ViewZoom { get; set; } = 1.0;

                // Entities imported from C# class definitions. Each holds a
                // hash of its property set; re-importing the same name with a
                // different hash bumps the version and triggers a sweep of
                // nodes derived from it (warnings on stale ports).
                public List<EntityDef> ImportedEntities { get; set; } = new();

                public Session() { }
            }

            public class EntityDef
            {
                public string Name { get; set; }
                public Dictionary<string, string> Properties { get; set; } = new();
                public string Hash { get; set; }
                public int Version { get; set; } = 1;

                // Pariah's serializer requires an explicit parameterless ctor.
                public EntityDef() { }
            }

            public class ConnectionWarning
            {
                public Node.Connection Connection { get; set; }
                public string Reason { get; set; }

                public ConnectionWarning() { }
            }

            /// <summary>Validates all required inputs and returns warnings for unconnected ones.</summary>
            public static List<string> ValidateRequiredPorts(Session session)
            {
                var warnings = new List<string>();
                var connectedInputs = session.Connections
                    .GroupBy(c => c.Node2UUID)
                    .ToDictionary(g => g.Key, g => g.Select(c => c.Node2Port).ToHashSet());

                int nodeIndex = 0;
                foreach (var node in session.Nodes)
                {
                    var connected = connectedInputs.TryGetValue(node.UUID, out var set) ? set : new HashSet<string>();
                    foreach (var input in node.Inputs.Where(i => i.Required))
                    {
                        if (!connected.Contains(input.Name))
                        {
                            warnings.Add($"Node \"{node.Title}\" (#{nodeIndex}): required input \"{input.Name}\" is not connected.");
                        }
                    }
                    nodeIndex++;
                }
                return warnings;
            }

            public class Chunk
            {
                public string Name { get; set; }
                public string Description { get; set; }
                public HashSet<string> NodeUUIDs { get; set; } = new();
                public float Left { get; set; }
                public float Top { get; set; }
                public float Width { get; set; }
                public float Height { get; set; }
                public uint BorderColor { get; set; }

                public Chunk() { }
            }

            public class Note
            {
                public string Name { get; set; }
                public string Description { get; set; }
                public float Left { get; set; }
                public float Top { get; set; }

                public Note() { }
            }

            public static async Task<List<ConnectionWarning>> Diagnose(Session session)
            {
                List<ConnectionWarning> warnings = new();
                foreach (var c in session.Connections)
                {
                    var n1 = session.Nodes.FirstOrDefault(n => n.UUID == c.Node1UUID);
                    var n2 = session.Nodes.FirstOrDefault(n => n.UUID == c.Node2UUID);

                    if (n1 == null || n2 == null)
                    {
                        warnings.Add(new ConnectionWarning { Connection = c, Reason = "Missing node reference" });
                        continue;
                    }
                    if (!n1.Outputs.Any(o => o.Name == c.Node1Port) && !n1.Inputs.Any(i => i.Name == c.Node1Port))
                        warnings.Add(new ConnectionWarning { Connection = c, Reason = "Node1 port missing" });

                    if (!n2.Inputs.Any(i => i.Name == c.Node2Port) && !n2.Outputs.Any(o => o.Name == c.Node2Port))
                        warnings.Add(new ConnectionWarning { Connection = c, Reason = "Node2 port missing" });
                }
                return warnings;
            }
        }

        // Operations

        public static class Operations
        {
            private static readonly JsonSerializerOptions _jsonOptions = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // Vector3 exposes X/Y/Z as public *fields*, not properties.
                // Without IncludeFields System.Text.Json writes {} for every
                // node position and reloads them as (0,0,0) — i.e. all nodes
                // collapse to the canvas origin on every load.
                IncludeFields = true
            };

            // Save / Load

            /// <summary>
            /// FIXED: was using Directory.Exists — now uses File.Exists.
            /// Also tries both with and without .json extension.
            /// </summary>
            public static bool CheckIfSessionFileExists(string fileName, string fileLocation)
            {
                var path1 = Path.Combine(fileLocation, fileName);
                var path2 = path1.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? path1 : path1 + ".json";
                return File.Exists(path1) || File.Exists(path2);
            }

            private static string ResolvePath(string fileName, string fileLocation)
            {
                var path = Path.Combine(fileLocation, fileName);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    path += ".json";
                return path;
            }

            public static async Task SaveSession(SessionData.Session session, string fileName, string fileLocation)
            {
                Directory.CreateDirectory(fileLocation);
                var path = ResolvePath(fileName, fileLocation);
                var json = JsonSerializer.Serialize(session, _jsonOptions);
                await File.WriteAllTextAsync(path, json);
            }

            public static async Task<SessionData.Session> LoadSession(string fileName, string fileLocation)
            {
                var path = ResolvePath(fileName, fileLocation);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Session file not found: {path}");

                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new SessionData.Session();

                var session = JsonSerializer.Deserialize<SessionData.Session>(json, _jsonOptions)
                    ?? new SessionData.Session();

                foreach (var node in session.Nodes)
                {
                    foreach (var input in node.Inputs)
                        input.Type = ResolveType(input.TypeName);
                    foreach (var output in node.Outputs)
                        output.Type = ResolveType(output.TypeName);
                }

                return session;
            }

            private static Type ResolveType(string typeName) => typeName switch
            {
                "System.Int32" => typeof(int),
                "System.Double" => typeof(double),
                "System.String" => typeof(string),
                "System.Boolean" => typeof(bool),
                _ => typeof(object)
            };

            // Node / Port manipulation

            public static void ConnectNode(SessionData.Session session, Node.Connection connection)
            {
                var node1 = session.Nodes.FirstOrDefault(n => n.UUID == connection.Node1UUID)
                    ?? throw new Exception("Node1 does not exist.");
                var node2 = session.Nodes.FirstOrDefault(n => n.UUID == connection.Node2UUID)
                    ?? throw new Exception("Node2 does not exist.");

                bool validPort1 = node1.Outputs.Any(o => o.Name == connection.Node1Port)
                               || node1.Inputs.Any(i => i.Name == connection.Node1Port);
                bool validPort2 = node2.Inputs.Any(i => i.Name == connection.Node2Port)
                               || node2.Outputs.Any(o => o.Name == connection.Node2Port);

                if (!validPort1 || !validPort2)
                    throw new Exception($"Invalid port(s): {connection.Node1Port} / {connection.Node2Port}");

                // Each input port accepts only ONE incoming connection. Drop
                // any existing wire targeting the same (Node2UUID, Node2Port)
                // before adding the new one — this turns "drag a new line into
                // a port" into a replace operation. Outputs can still fan out.
                session.Connections.RemoveWhere(c =>
                    c.Node2UUID == connection.Node2UUID &&
                    c.Node2Port == connection.Node2Port);

                session.Connections.Add(connection);
            }

            public static void DisconnectNode(SessionData.Session session, Node.Connection connection)
            {
                var existing = session.Connections.FirstOrDefault(c =>
                    c.Node1UUID == connection.Node1UUID && c.Node2UUID == connection.Node2UUID &&
                    c.Node1Port == connection.Node1Port && c.Node2Port == connection.Node2Port)
                    ?? throw new Exception("Connection does not exist.");
                session.Connections.Remove(existing);
            }

            /// <summary>
            /// Renames a port on a specific node, also updating all connections that reference it.
            /// </summary>
            public static void RenamePort(SessionData.Session session, string nodeUUID, string oldPort, string newPort)
            {
                var node = session.Nodes.FirstOrDefault(n => n.UUID == nodeUUID)
                    ?? throw new Exception("Node not found.");

                node.Inputs = node.Inputs.Select(i =>
                    i.Name == oldPort ? new Input(newPort, i.Type, i.SemanticType, i.Required, i.CustomTypeName) : i
                ).ToHashSet();

                node.Outputs = node.Outputs.Select(o =>
                    o.Name == oldPort ? new Output(newPort, o.Type, o.SemanticType, o.CustomTypeName) : o
                ).ToHashSet();

                // Update all connections that reference this port
                foreach (var conn in session.Connections)
                {
                    if (conn.Node1UUID == nodeUUID && conn.Node1Port == oldPort) conn.Node1Port = newPort;
                    if (conn.Node2UUID == nodeUUID && conn.Node2Port == oldPort) conn.Node2Port = newPort;
                }
            }

            /// <summary>
            /// Replaces all nodes matching <paramref name="findTitle"/> with clones of <paramref name="replacement"/>,
            /// remapping port <paramref name="findPort"/> → <paramref name="replacePort"/> in all connections.
            /// Connections on other ports are preserved where the replacement node has matching port names.
            /// </summary>
            public static ReplaceResult ReplaceNodesByTitle(
                SessionData.Session session,
                string findTitle,
                string findPort,
                BareNode replacement,
                string replacePort)
            {
                var result = new ReplaceResult();
                var targets = session.Nodes.Where(n => n.Title == findTitle).ToList();

                foreach (var target in targets)
                {
                    // Clone replacement node with new UUID
                    var newNode = new BareNode
                    {
                        Title = replacement.Title,
                        Description = replacement.Description,
                        Inputs = new HashSet<Input>(replacement.Inputs.Select(i =>
                            new Input(i.Name, i.Type, i.SemanticType, i.Required, i.CustomTypeName))),
                        Outputs = new HashSet<Output>(replacement.Outputs.Select(o =>
                            new Output(o.Name, o.Type, o.SemanticType, o.CustomTypeName))),
                        UUID = Guid.NewGuid().ToString(),
                        Logic = replacement.Logic,
                        SyncType = replacement.SyncType
                    };

                    // Remap connections
                    foreach (var conn in session.Connections.Where(c => c.Node1UUID == target.UUID || c.Node2UUID == target.UUID))
                    {
                        if (conn.Node1UUID == target.UUID)
                        {
                            conn.Node1UUID = newNode.UUID;
                            if (conn.Node1Port == findPort) conn.Node1Port = replacePort;
                        }
                        if (conn.Node2UUID == target.UUID)
                        {
                            conn.Node2UUID = newNode.UUID;
                            if (conn.Node2Port == findPort) conn.Node2Port = replacePort;
                        }
                    }

                    // Remove bad connections where the new node doesn't have the referenced port
                    var badConns = session.Connections.Where(c =>
                        (c.Node1UUID == newNode.UUID && !newNode.Outputs.Any(o => o.Name == c.Node1Port) && !newNode.Inputs.Any(i => i.Name == c.Node1Port)) ||
                        (c.Node2UUID == newNode.UUID && !newNode.Inputs.Any(i => i.Name == c.Node2Port) && !newNode.Outputs.Any(o => o.Name == c.Node2Port))
                    ).ToList();

                    foreach (var bc in badConns)
                    {
                        session.Connections.Remove(bc);
                        result.DroppedConnections.Add(bc);
                    }

                    session.Nodes.Remove(target);
                    session.Nodes.Add(newNode);

                    // Preserve canvas position
                    if (session.NodePositions.TryGetValue(target.UUID, out var pos))
                    {
                        session.NodePositions.Remove(target.UUID);
                        session.NodePositions[newNode.UUID] = pos;
                    }

                    result.ReplacedCount++;
                    result.OldToNewUUID[target.UUID] = newNode.UUID;
                }

                return result;
            }

            public class ReplaceResult
            {
                public int ReplacedCount { get; set; }
                public Dictionary<string, string> OldToNewUUID { get; } = new();
                public List<Node.Connection> DroppedConnections { get; } = new();

                public ReplaceResult() { }
            }

            public static List<string> GetNodesWithPort(SessionData.Session session, string portName)
            {
                return session.Nodes
                    .Where(n => n.Inputs.Any(i => i.Name == portName) || n.Outputs.Any(o => o.Name == portName))
                    .Select(n => n.UUID)
                    .ToList();
            }

            public static List<Node.Connection> GetBadConnections(SessionData.Session oldSession, SessionData.Session newSession)
            {
                return oldSession.Connections.Where(connection =>
                {
                    var n1 = newSession.Nodes.FirstOrDefault(n => n.UUID == connection.Node1UUID);
                    var n2 = newSession.Nodes.FirstOrDefault(n => n.UUID == connection.Node2UUID);
                    if (n1 == null || n2 == null) return true;

                    bool port1Ok = n1.Outputs.Any(o => o.Name == connection.Node1Port) || n1.Inputs.Any(i => i.Name == connection.Node1Port);
                    bool port2Ok = n2.Inputs.Any(i => i.Name == connection.Node2Port) || n2.Outputs.Any(o => o.Name == connection.Node2Port);
                    return !port1Ok || !port2Ok;
                }).ToList();
            }
        }

        // Compiler — generates actual drop-in C# code

        public static class Compiler
        {
            /// <summary>
            /// Compiles a session into a full C# script. The output is shaped as:
            ///
            ///     // header
            ///     using ...; using ...;        ← collected (defaults ∪ session.Usings ∪
            ///                                    using-lines hoisted out of every node's Logic)
            ///     public static class GeneratedScript
            ///     {
            ///         // one method per *custom* node, deduped by SafeId(Title)
            ///         public static T MyMethod(...) { ...node.Logic... }
            ///
            ///         // the main entry point, body driven by topological order
            ///         public static T DoACoolThing(...) { ... }
            ///     }
            /// </summary>
            public static string CompileToScript(SessionData.Session session)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"// {session.Name ?? "Unnamed"}");
                sb.AppendLine($"// {session.Nodes?.Count ?? 0} node(s), {session.Connections?.Count ?? 0} connection(s)");
                sb.AppendLine();

                // 1) Usings — defaults ∪ session-level ∪ those hoisted out of any node Logic
                foreach (var u in CollectUsings(session))
                    sb.AppendLine($"using {u};");
                sb.AppendLine();

                // 2) Wrapper class
                var className = SafeId(string.IsNullOrWhiteSpace(session.Name) ? "Generated" : session.Name);
                if (!className.EndsWith("Script", StringComparison.Ordinal)) className += "Script";
                sb.AppendLine($"public static class {className}");
                sb.AppendLine("{");

                // 3) Custom-node methods (one per unique custom title)
                EmitCustomNodeMethods(sb, session);

                // 4) Main entry point (existing topological-order code-gen)
                EmitMainMethod(sb, session);

                sb.AppendLine("}");
                return sb.ToString();
            }

            // Script-level scaffolding

            // Built-in node titles handled by named cases inside GenerateNodeCode.
            // Anything not in this set falls through to the "default" case and
            // is therefore a candidate for being lifted into a top-level method.
            private static readonly HashSet<string> _builtinTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "get variable", "set variable", "event input", "set output",
                "add", "subtract", "multiply", "divide",
                "if", "and", "or", "not",
                "concat", "format", "weburl", "custom",
                "ef: query all", "ef: query where", "ef: find by id",
                "ef: insert", "ef: update", "ef: delete",
                "stdb: insert", "stdb: delete", "stdb: filter by id",
                "start", "end",
                // Literals
                "string literal", "int literal", "float literal", "bool literal", "null",
                "json literal", "connection string literal", "predicate literal", "custom input", "custom literal",
                "expose", "run after",
                "equals", "not equals", "less than", "greater than", "less or equal", "greater or equal",
                "lambda",
                "pg: bulk insert", "pg: prepare", "pg: run prepared", "pg: batch execute",
                "pg: notify", "pg: listen",
                // EF Core: Easy
                "db: open", "db: save", "db: close",
                "db: get all", "db: get one by id", "db: get where", "db: get first",
                "db: count", "db: add", "db: add and save", "db: update and save",
                "db: remove and save", "db: exists",
                "db: begin tx", "db: commit tx", "db: rollback tx",
                "where: equals", "where: not equals", "where: greater", "where: less",
                "where: contains", "where: and", "where: or",
                "db: order by", "db: order by desc", "db: page", "db: include",
                "db: rls enable", "db: rls disable", "db: rls force",
                "db: rls create policy", "db: rls drop policy",
                "db: rls set user", "db: rls reset user", "db: raw sql",
                "sdb: connect", "sdb: disconnect", "sdb: subscribe", "sdb: call reducer",
                "sdb: iter table", "sdb: find by pk",
                "sdb: on insert", "sdb: on update", "sdb: on delete",
                // HTTP / HTTP/2
                "http: new client", "http: get", "http: post json", "http: put json",
                "http: delete", "http: send",
                "http: read json", "http: read string", "http: status code",
                "http: set bearer token", "http: set header", "http: ensure success",
                // Object accessors
                "cast",
                // Postgres
                "pg: connect", "pg: query", "pg: query first", "pg: execute",
                "pg: insert", "pg: update by id", "pg: delete by id", "pg: count",
                "pg: begin tx", "pg: commit tx", "pg: rollback tx", "pg: close",
                // Auth (Pariah)
                "auth: setup", "auth: sign up", "auth: login", "auth: validate session",
                "auth: logout", "auth: reset password", "auth: hash password",
                "auth: verify password", "auth: generate password",
                "auth: list users", "auth: remove account",
                // SSO (Pariah)
                "sso: create system", "sso: connect app", "sso: verify session integrity",
                "sso: get paths", "sso: add blacklist", "sso: remove blacklist",
                "sso: device master secret",
                // Marketplace
                "market: create listing", "market: cancel listing", "market: buy listing",
                "market: search listings", "market: get user listings",
                "market: get wallet", "market: add funds", "market: withdraw funds",
                "market: get inventory", "market: transfer item",
            };

            // Titles whose generated calls reference Npgsql / Dapper. If any of
            // these appear in the session we auto-add the matching using lines
            // so the generated script is drop-in compilable.
            private static readonly HashSet<string> _pgTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "pg: connect", "pg: query", "pg: query first", "pg: execute",
                "pg: insert", "pg: update by id", "pg: delete by id", "pg: count",
                "pg: begin tx", "pg: commit tx", "pg: rollback tx", "pg: close",
                "pg: bulk insert", "pg: prepare", "pg: run prepared", "pg: batch execute",
                "pg: notify", "pg: listen",
            };

            // Titles that need the Pariah_Cybersecurity namespace.
            private static readonly HashSet<string> _pariahTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "auth: setup", "auth: sign up", "auth: login", "auth: validate session",
                "auth: logout", "auth: reset password", "auth: hash password",
                "auth: verify password", "auth: generate password",
                "auth: list users", "auth: remove account",
                "sso: create system", "sso: connect app", "sso: verify session integrity",
                "sso: get paths", "sso: add blacklist", "sso: remove blacklist",
                "sso: device master secret",
            };

            // Titles that need EF Core.
            private static readonly HashSet<string> _efTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "ef: query all", "ef: query where", "ef: find by id",
                "ef: insert", "ef: update", "ef: delete",
                "market: create listing", "market: cancel listing", "market: buy listing",
                "market: search listings", "market: get user listings",
                "market: get wallet", "market: add funds", "market: withdraw funds",
                "market: get inventory", "market: transfer item",
                "db: open", "db: save", "db: close",
                "db: get all", "db: get one by id", "db: get where", "db: get first",
                "db: count", "db: add", "db: add and save", "db: update and save",
                "db: remove and save", "db: exists",
                "db: begin tx", "db: commit tx", "db: rollback tx",
                "where: equals", "where: not equals", "where: greater", "where: less",
                "where: contains", "where: and", "where: or",
                "db: order by", "db: order by desc", "db: page", "db: include",
                "db: rls enable", "db: rls disable", "db: rls force",
                "db: rls create policy", "db: rls drop policy",
                "db: rls set user", "db: rls reset user", "db: raw sql",
            };

            // SpacetimeDB titles → auto-add the `SpacetimeDB.Types` namespace.
            private static readonly HashSet<string> _sdbTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "sdb: connect", "sdb: disconnect", "sdb: subscribe", "sdb: call reducer",
                "sdb: iter table", "sdb: find by pk",
                "sdb: on insert", "sdb: on update", "sdb: on delete",
            };

            // HTTP titles → auto-add System.Net.Http and System.Net.Http.Json.
            private static readonly HashSet<string> _httpTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "http: new client", "http: get", "http: post json", "http: put json",
                "http: delete", "http: send",
                "http: read json", "http: read string", "http: status code",
                "http: set bearer token", "http: set header", "http: ensure success",
            };

            private static SortedSet<string> CollectUsings(SessionData.Session session)
            {
                // Defaults that virtually any generated script needs.
                var set = new SortedSet<string>(StringComparer.Ordinal)
                {
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Threading.Tasks",
                };

                void AddRaw(string raw)
                {
                    if (string.IsNullOrWhiteSpace(raw)) return;
                    var s = raw.Trim();
                    if (s.StartsWith("using ", StringComparison.Ordinal)) s = s.Substring(6);
                    s = s.TrimEnd(';').Trim();
                    if (s.Length > 0) set.Add(s);
                }

                if (session.Usings != null)
                    foreach (var u in session.Usings) AddRaw(u);

                IEnumerable<BareNode> all = session.Nodes ?? (IEnumerable<BareNode>)Array.Empty<BareNode>();
                if (session.CustomScripts != null) all = all.Concat(session.CustomScripts);

                bool hasPg = false, hasPariah = false, hasEf = false, hasSdb = false, hasHttp = false;
                foreach (var n in all)
                {
                    foreach (var u in ExtractUsingLines(n.Logic)) set.Add(u);
                    if (n.Title == null) continue;
                    var t = n.Title.Trim();
                    if (_pgTitles.Contains(t))     hasPg     = true;
                    if (_pariahTitles.Contains(t)) hasPariah = true;
                    if (_efTitles.Contains(t))     hasEf     = true;
                    if (_sdbTitles.Contains(t))    hasSdb    = true;
                    if (_httpTitles.Contains(t))   hasHttp   = true;
                }
                if (hasPg)
                {
                    set.Add("Npgsql");
                    set.Add("Dapper");
                }
                if (hasPariah)
                {
                    set.Add("Pariah_Cybersecurity");
                    set.Add("static Pariah_Cybersecurity.DataHandler");
                    set.Add("static Pariah_Cybersecurity.DataHandler.SaltAndHashing");
                }
                if (hasEf)
                {
                    set.Add("Microsoft.EntityFrameworkCore");
                    set.Add("Microsoft.EntityFrameworkCore.Storage");
                }
                if (hasSdb)
                {
                    set.Add("SpacetimeDB");
                    set.Add("SpacetimeDB.Types");
                }
                if (hasHttp)
                {
                    set.Add("System.Net");
                    set.Add("System.Net.Http");
                    set.Add("System.Net.Http.Json");
                    set.Add("System.Net.Http.Headers");
                }

                return set;
            }

            private static readonly Regex _usingLineRx =
                new(@"^[ \t]*using[ \t]+([\w\.]+)[ \t]*;[ \t]*\r?\n?", RegexOptions.Multiline | RegexOptions.Compiled);

            private static IEnumerable<string> ExtractUsingLines(string code)
            {
                if (string.IsNullOrWhiteSpace(code)) yield break;
                foreach (Match m in _usingLineRx.Matches(code))
                    yield return m.Groups[1].Value;
            }

            private static string StripUsingLines(string code) =>
                string.IsNullOrWhiteSpace(code) ? code : _usingLineRx.Replace(code, "");

            private static bool IsPlaceholderLogic(string logic) =>
                   string.IsNullOrWhiteSpace(logic)
                || (logic.TrimStart().StartsWith("//", StringComparison.Ordinal)
                    && !logic.Contains('\n')
                    && !logic.Contains(';')
                    && !logic.Contains('{'));

            private static void EmitCustomNodeMethods(StringBuilder sb, SessionData.Session session)
            {
                var emitted = new HashSet<string>(StringComparer.Ordinal);
                IEnumerable<BareNode> sources = session.Nodes ?? (IEnumerable<BareNode>)Array.Empty<BareNode>();
                if (session.CustomScripts != null) sources = sources.Concat(session.CustomScripts);

                foreach (var node in sources)
                {
                    if (string.IsNullOrWhiteSpace(node.Title)) continue;
                    if (_builtinTitles.Contains(node.Title.Trim())) continue;

                    var name = SafeId(node.Title);
                    if (!emitted.Add(name)) continue; // already wrote a definition for this title

                    EmitCustomNodeMethod(sb, node, name);
                    sb.AppendLine();
                }
            }

            private static void EmitCustomNodeMethod(StringBuilder sb, BareNode node, string safeName)
            {
                bool isAsync = node.IsAsync;

                string returnType;
                if (node.Outputs == null || node.Outputs.Count == 0) returnType = "void";
                else if (node.Outputs.Count == 1)
                {
                    var o = node.Outputs.First();
                    returnType = SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName);
                }
                else
                {
                    returnType = "(" + string.Join(", ",
                        node.Outputs.Select(o => SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName))) + ")";
                }

                string modifier;
                if (isAsync)
                    modifier = returnType == "void" ? "public static async Task" : $"public static async Task<{returnType}>";
                else
                    modifier = $"public static {returnType}";

                var args = string.Join(", ", (node.Inputs ?? new()).Select(i =>
                    $"{SemanticTypeToCSharp(i.SemanticType, i.CustomTypeName)} {SafeId(i.Name)}"));

                if (!string.IsNullOrWhiteSpace(node.Description))
                {
                    sb.AppendLine($"    /// <summary>{System.Security.SecurityElement.Escape(node.Description)?.Trim()}</summary>");
                }
                sb.AppendLine($"    {modifier} {safeName}({args})");
                sb.AppendLine("    {");

                if (IsPlaceholderLogic(node.Logic))
                {
                    sb.AppendLine("        // TODO: implement");
                    if (returnType != "void") sb.AppendLine("        return default;");
                }
                else
                {
                    var body = StripUsingLines(node.Logic).Trim();
                    foreach (var line in body.Split('\n'))
                        sb.AppendLine("        " + line.TrimEnd());
                }

                sb.AppendLine("    }");
            }

            private static void EmitMainMethod(StringBuilder sb, SessionData.Session session)
            {
                bool isAsync = session.IsAsync || (session.Nodes?.Any(n => n.IsAsync) ?? false);
                string returnType = GetReturnType(session);

                var inputNodes  = session.Nodes?.Where(n => n.Title == "Event Input").ToList() ?? new();
                var outputNodes = session.Nodes?.Where(n => n.Title == "Set Output").ToList() ?? new();
                // Custom Input nodes also become method parameters — their CLR
                // type comes from the CUSTOMINPUT(<Type>) marker stored in Logic.
                var customInputNodes = session.Nodes?.Where(n => n.Title == "Custom Input").ToList() ?? new();

                var paramList = inputNodes
                    .SelectMany(n => n.Outputs)
                    .Select(o => $"{SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName)} {SafeId(o.Name)}")
                    .ToList();
                foreach (var n in customInputNodes)
                {
                    var port = n.Outputs.FirstOrDefault();
                    if (port == null) continue;
                    var typeName = !string.IsNullOrEmpty(port.CustomTypeName)
                        ? port.CustomTypeName
                        : SemanticTypeToCSharp(port.SemanticType, port.CustomTypeName);
                    paramList.Add($"{typeName} {SafeId(port.Name)}");
                }

                string methodModifier;
                if (isAsync)
                    methodModifier = returnType == "void"
                        ? "public static async Task"
                        : $"public static async Task<{returnType}>";
                else
                    methodModifier = $"public static {returnType}";

                var funcName = SafeId(session.FunctionName ?? "DoACoolThing");
                var parameters = string.Join(", ", paramList);

                sb.AppendLine($"    {methodModifier} {funcName}({parameters})");
                sb.AppendLine("    {");

                if (session.Nodes == null || session.Nodes.Count == 0)
                {
                    sb.AppendLine("        // No nodes — add nodes to generate code");
                    sb.AppendLine("    }");
                    return;
                }

                var portWarnings = SessionData.ValidateRequiredPorts(session);
                foreach (var w in portWarnings)
                    sb.AppendLine($"        // WARNING: {w}");
                if (portWarnings.Any()) sb.AppendLine();

                var nodes = session.Nodes.ToDictionary(n => n.UUID);
                var reverseMap = session.Connections
                    .GroupBy(c => c.Node2UUID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Predicate map gets reset per compile so re-entry doesn't
                // leak lambda text from the previous run.
                _predicateExpressions = new Dictionary<string, string>();

                List<string> order;
                try { order = TopologicalSort(session); }
                catch { order = session.Nodes.Select(n => n.UUID).ToList(); }

                var declaredVars = new HashSet<string>();

                foreach (var uuid in order)
                {
                    if (!nodes.TryGetValue(uuid, out var node)) continue;

                    var incoming = reverseMap.TryGetValue(uuid, out var inc) ? inc : new();
                    var inputValues = incoming.ToDictionary(c => c.Node2Port, c =>
                    {
                        // If the upstream node already emitted a predicate-shaped
                        // expression for this connection, splat the lambda
                        // directly in instead of using the upstream variable name.
                        // This is what lets `DB: Get First` see `g => g.Title == x`
                        // instead of the upstream `Where_Equals_xxxx_Predicate`.
                        if (_predicateExpressions != null &&
                            _predicateExpressions.TryGetValue($"{c.Node1UUID}:{c.Node1Port}", out var pe))
                            return pe;
                        var fromNode = nodes.TryGetValue(c.Node1UUID, out var fn) ? fn : null;
                        return fromNode != null ? NodeVarName(fromNode, c.Node1Port) : "null";
                    });

                    // Per-port context the codegen cases can read.
                    var inputTypes    = new Dictionary<string, string>();
                    var inputLiterals = new Dictionary<string, string>();
                    var inputUpstream = new Dictionary<string, string>();
                    foreach (var c in incoming)
                    {
                        if (!nodes.TryGetValue(c.Node1UUID, out var fn)) continue;
                        var op = fn.Outputs?.FirstOrDefault(o => o.Name == c.Node1Port);
                        if (op != null && !string.IsNullOrEmpty(op.CustomTypeName))
                            inputTypes[c.Node2Port] = op.CustomTypeName;
                        var lit = ExtractLiteralValue(fn.Logic);
                        if (!string.IsNullOrEmpty(lit)) inputLiterals[c.Node2Port] = lit;
                        if (!string.IsNullOrEmpty(fn.Title))
                            inputUpstream[c.Node2Port] = fn.Title.Trim().ToLowerInvariant();
                    }
                    _inputTypeContext    = inputTypes;
                    _inputLiteralContext = inputLiterals;
                    _inputUpstreamTitle  = inputUpstream;
                    _currentNodeUuid     = node.UUID;

                    string code = GenerateNodeCode(node, inputValues, declaredVars, isAsync);
                    _inputTypeContext    = null;
                    _inputLiteralContext = null;
                    _inputUpstreamTitle  = null;
                    _currentNodeUuid     = null;
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        sb.AppendLine($"        // {node.Title}");
                        foreach (var line in code.Split('\n'))
                            if (!string.IsNullOrWhiteSpace(line))
                                sb.AppendLine("        " + line.TrimEnd());
                        sb.AppendLine();
                    }
                }

                if (returnType != "void")
                {
                    var outVars = outputNodes.SelectMany(n => n.Inputs).Select(i => SafeId(i.Name)).ToList();
                    if (outVars.Count == 1)      sb.AppendLine($"        return {outVars[0]};");
                    else if (outVars.Count > 1)  sb.AppendLine($"        return ({string.Join(", ", outVars)});");
                }

                sb.AppendLine("    }");
            }

            private static string GetReturnType(SessionData.Session session)
            {
                var outputNodes = session.Nodes?.Where(n => n.Title == "Set Output").ToList() ?? new();
                if (!outputNodes.Any()) return "void";
                var outputs = outputNodes.SelectMany(n => n.Inputs).ToList();
                if (outputs.Count == 1) return SemanticTypeToCSharp(outputs[0].SemanticType, outputs[0].CustomTypeName);
                if (outputs.Count > 1) return $"({string.Join(", ", outputs.Select(o => SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName)))})";
                return "void";
            }

            private static string GenerateNodeCode(BareNode node, Dictionary<string, string> inputValues, HashSet<string> declared, bool inAsync)
            {
                var sb = new StringBuilder();
                var id = NodeShortId(node);
                var title = node.Title?.ToLower().Trim() ?? "";

                if (node.Logic?.StartsWith("ERROR:") == true)
                {
                    sb.AppendLine($"// ERROR in node \"{node.Title}\": {node.Logic.Substring(6).Trim()}");
                    return sb.ToString();
                }

                switch (title)
                {
                    case "get variable":
                    {
                        var varName = inputValues.TryGetValue("Name", out var vn) ? vn : $"\"{SafeId(node.Title)}\"";
                        var outVar = NodeVarName(node, "Value");
                        Declare(sb, declared, outVar, "var", $"variables[{varName}]");
                        break;
                    }
                    case "set variable":
                    {
                        var varName = inputValues.TryGetValue("Name", out var vn) ? vn : $"\"{SafeId(node.Title)}\"";
                        var value = inputValues.TryGetValue("Value", out var val) ? val : "null";
                        sb.AppendLine($"variables[{varName}] = {value};");
                        break;
                    }
                    case "event input":
                    {
                        // Already emitted as parameters — just emit a comment
                        sb.AppendLine($"// Event fired — inputs available as parameters");
                        break;
                    }
                    case "set output":
                    {
                        foreach (var inp in node.Inputs)
                        {
                            var val = inputValues.TryGetValue(inp.Name, out var v) ? v : "default";
                            var outVar = SafeId(inp.Name);
                            Declare(sb, declared, outVar, SemanticTypeToCSharp(inp.SemanticType, inp.CustomTypeName), val);
                        }
                        break;
                    }
                    case "add":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{a} + {b}");
                        break;
                    }
                    case "subtract":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{a} - {b}");
                        break;
                    }
                    case "multiply":
                    {
                        var a = Inp(inputValues, "A", "1");
                        var b = Inp(inputValues, "B", "1");
                        DeclareExpr(sb, declared, node, "Result", $"{a} * {b}");
                        break;
                    }
                    case "divide":
                    {
                        var a = Inp(inputValues, "A", "1");
                        var b = Inp(inputValues, "B", "1");
                        sb.AppendLine($"if ({b} == 0) throw new DivideByZeroException(\"Divide node '{node.Title}': divisor is zero.\");");
                        DeclareExpr(sb, declared, node, "Result", $"(double){a} / {b}");
                        break;
                    }
                    case "if":
                    {
                        var cond = Inp(inputValues, "Condition", "null");
                        sb.AppendLine($"if ({TruthyExpr(cond)})");
                        sb.AppendLine("{");
                        sb.AppendLine("    // True branch — connect nodes here");
                        sb.AppendLine("}");
                        sb.AppendLine("else");
                        sb.AppendLine("{");
                        sb.AppendLine("    // False branch — connect nodes here");
                        sb.AppendLine("}");
                        break;
                    }
                    case "type literal":
                        {
                            var lit = ExtractLiteralValue(node.Logic) ?? "object";
                            // Clean up the type name - remove extra quotes, etc.
                            var typeName = StripQuotesExpr(lit.Trim());
                            // Generate typeof(Game) instead of a string
                            DeclareExpr(sb, declared, node, "Type", $"typeof({typeName})");
                            break;
                        }
                    case "and":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{TruthyExpr(a)} && {TruthyExpr(b)}");
                        break;
                    }
                    case "or":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{TruthyExpr(a)} || {TruthyExpr(b)}");
                        break;
                    }
                    case "not":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", $"!({TruthyExpr(v)})");
                        break;
                    }
                    case "expose":
                    {
                        var prop = ExtractLiteralValue(node.Logic);
                        if (string.IsNullOrWhiteSpace(prop)) prop = "Property";
                        var obj = Inp(inputValues, "Object", "null");
                        DeclareExpr(sb, declared, node, "Value",
                            $"({obj})?.GetType().GetProperty(\"{EscapeString(prop.Trim())}\")?.GetValue({obj})");
                        break;
                    }
                    case "cast":
                    {
                        var obj = Inp(inputValues, "Object", "null");
                        var targetType = TypeFromConnectedPort("Type") ?? ExtractLiteralValue(node.Logic) ?? "object";
                        targetType = targetType.Trim('"', '\'');
                        DeclareExpr(sb, declared, node, "Result",
                            targetType == "object" ? $"({obj})" : $"({targetType}){obj}");
                        break;
                    }
                    case "run after":
                    {
                        var after = Inp(inputValues, "After", "null");
                        DeclareExpr(sb, declared, node, "Then", after);
                        break;
                    }

                    // New Logic Nodes - easier to understand
                    case "logic: is null":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{v} == null");
                        break;
                    }
                    case "logic: is not null":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{v} != null");
                        break;
                    }
                    case "logic: convert to string":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", $"({v})?.ToString()");
                        break;
                    }
                    case "logic: convert to int":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"int.TryParse({v}?.ToString(), out var __i) ? __i : 0");
                        break;
                    }
                    case "logic: try parse":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", 
                            $"int.TryParse({v}?.ToString(), out var __r) ? __r : default(int?)");
                        break;
                    }
                    case "logic: string is empty":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Result", $"string.IsNullOrEmpty({v})");
                        break;
                    }
                    case "logic: string contains":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        var sub = Inp(inputValues, "Substring", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.Contains({sub})");
                        break;
                    }
                    case "logic: string starts with":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        var sub = Inp(inputValues, "Substring", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.StartsWith({sub})");
                        break;
                    }
                    case "logic: string ends with":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        var sub = Inp(inputValues, "Substring", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.EndsWith({sub})");
                        break;
                    }
                    case "logic: string replace":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        var old = Inp(inputValues, "OldValue", "\"\"");
                        var newv = Inp(inputValues, "NewValue", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.Replace({old}, {newv})");
                        break;
                    }
                    case "logic: string to lower":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.ToLower()");
                        break;
                    }
                    case "logic: string to upper":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.ToUpper()");
                        break;
                    }
                    case "logic: string trim":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.Trim()");
                        break;
                    }
                    case "logic: string split":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        var sep = Inp(inputValues, "Separator", "\",\"");
                        DeclareExpr(sb, declared, node, "Result", $"{s}.Split({sep})");
                        break;
                    }
                    case "logic: string join":
                    {
                        var arr = Inp(inputValues, "Array", "Array.Empty<string>()");
                        var sep = Inp(inputValues, "Separator", "\",\"");
                        DeclareExpr(sb, declared, node, "Result", $"string.Join({sep}, {arr})");
                        break;
                    }
                    case "logic: array length":
                    {
                        var arr = Inp(inputValues, "Array", "null");
                        DeclareExpr(sb, declared, node, "Result", $"({arr})?.Length ?? 0");
                        break;
                    }
                    case "logic: array first":
                    {
                        var arr = Inp(inputValues, "Array", "null");
                        DeclareExpr(sb, declared, node, "Result", $"({arr})?.FirstOrDefault()");
                        break;
                    }
                    case "logic: array last":
                    {
                        var arr = Inp(inputValues, "Array", "null");
                        DeclareExpr(sb, declared, node, "Result", $"({arr})?.LastOrDefault()");
                        break;
                    }
                    case "logic: array contains":
                    {
                        var arr = Inp(inputValues, "Array", "null");
                        var item = Inp(inputValues, "Item", "null");
                        DeclareExpr(sb, declared, node, "Result", $"({arr})?.Contains({item}) ?? false");
                        break;
                    }
                    case "logic: math: abs":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Abs({v})");
                        break;
                    }
                    case "logic: math: min":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Min({a}, {b})");
                        break;
                    }
                    case "logic: math: max":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Max({a}, {b})");
                        break;
                    }
                    case "logic: math: round":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Round({v})");
                        break;
                    }
                    case "logic: math: floor":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Floor({v})");
                        break;
                    }
                    case "logic: math: ceiling":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Ceiling({v})");
                        break;
                    }
                    case "logic: math: power":
                    {
                        var baseNum = Inp(inputValues, "Base", "0");
                        var exp = Inp(inputValues, "Exponent", "1");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Pow({baseNum}, {exp})");
                        break;
                    }
                    case "logic: math: sqrt":
                    {
                        var v = Inp(inputValues, "Value", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Math.Sqrt({v})");
                        break;
                    }
                    case "logic: date: now":
                    {
                        DeclareExpr(sb, declared, node, "Value", "DateTime.Now");
                        break;
                    }
                    case "logic: date: utc now":
                    {
                        DeclareExpr(sb, declared, node, "Value", "DateTime.UtcNow");
                        break;
                    }
                    case "logic: date: today":
                    {
                        DeclareExpr(sb, declared, node, "Value", "DateTime.Today");
                        break;
                    }
                    case "logic: date: add days":
                    {
                        var dt = Inp(inputValues, "Date", "DateTime.Now");
                        var days = Inp(inputValues, "Days", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{dt}.AddDays({days})");
                        break;
                    }
                    case "logic: date: add hours":
                    {
                        var dt = Inp(inputValues, "Date", "DateTime.Now");
                        var hours = Inp(inputValues, "Hours", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{dt}.AddHours({hours})");
                        break;
                    }
                    case "logic: date: add minutes":
                    {
                        var dt = Inp(inputValues, "Date", "DateTime.Now");
                        var mins = Inp(inputValues, "Minutes", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{dt}.AddMinutes({mins})");
                        break;
                    }
                    case "logic: date: format":
                    {
                        var dt = Inp(inputValues, "Date", "DateTime.Now");
                        var fmt = Inp(inputValues, "Format", "\"yyyy-MM-dd\"");
                        DeclareExpr(sb, declared, node, "Result", $"{dt}.ToString({fmt})");
                        break;
                    }
                    case "logic: date: parse":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"DateTime.TryParse({s}, out var __d) ? __d : default(DateTime?)");
                        break;
                    }
                    case "logic: guid: new":
                    {
                        DeclareExpr(sb, declared, node, "Value", "Guid.NewGuid()");
                        break;
                    }
                    case "logic: guid: empty":
                    {
                        DeclareExpr(sb, declared, node, "Value", "Guid.Empty");
                        break;
                    }
                    case "logic: guid: parse":
                    {
                        var s = Inp(inputValues, "String", "\"\"");
                        DeclareExpr(sb, declared, node, "Result", $"Guid.TryParse({s}, out var __g) ? __g : default(Guid?)");
                        break;
                    }
                    case "logic: coalesce":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{a} ?? {b}");
                        break;
                    }
                    case "logic: ternary":
                    {
                        var cond = Inp(inputValues, "Condition", "false");
                        var trueVal = Inp(inputValues, "True", "null");
                        var falseVal = Inp(inputValues, "False", "null");
                        DeclareExpr(sb, declared, node, "Result", $"{cond} ? {trueVal} : {falseVal}");
                        break;
                    }
                    case "logic: switch":
                    {
                        // Multi-way switch/pattern matching
                        var value = Inp(inputValues, "Value", "null");
                        var cases = inputValues.Where(kv => kv.Key.StartsWith("Case")).ToList();
                        var defaultVal = Inp(inputValues, "Default", "null");
                        
                        string switchExpr = $"((object){value}) switch {{";
                        foreach (var c in cases)
                        {
                            var caseVal = c.Value;
                            var resultVal = Inp(inputValues, $"Result{c.Key}", "null");
                            switchExpr += $" {(caseVal == "\"default\"" ? "_" : caseVal)} => {resultVal}, ";
                        }
                        switchExpr += $"_ => {defaultVal}}}";
                        DeclareExpr(sb, declared, node, "Result", switchExpr);
                        break;
                    }

                    // Comparisons — work on any object via Comparer<object>.Default.
                    case "equals":
                    case "not equals":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        var op = title == "equals" ? "" : "!";
                        DeclareExpr(sb, declared, node, "Result",
                            $"{op}object.Equals({a}, {b})");
                        break;
                    }
                    case "less than":
                    case "greater than":
                    case "less or equal":
                    case "greater or equal":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        var op = title switch
                        {
                            "less than"        => "< 0",
                            "greater than"     => "> 0",
                            "less or equal"    => "<= 0",
                            _                   => ">= 0"
                        };
                        DeclareExpr(sb, declared, node, "Result",
                            $"System.Collections.Generic.Comparer<object>.Default.Compare({a}, {b}) {op}");
                        break;
                    }

                    case "lambda":
                    {
                        // Lambda node: takes parameter name and body expression
                        // Body can come from wire or from inline logic
                        var paramName = ExtractLiteralValue(node.Logic) ?? "x";
                        var bodyInput = inputValues.TryGetValue("Body", out var b) ? b : "true";
                        
                        // Check if body is already a lambda (from upstream lambda node)
                        if (!string.IsNullOrEmpty(bodyInput) && bodyInput.Contains("=>"))
                        {
                            // Pass through existing lambda
                            DeclareExpr(sb, declared, node, "Lambda", bodyInput);
                        }
                        else
                        {
                            // Wrap body in new lambda
                            DeclareExpr(sb, declared, node, "Lambda",
                                $"({paramName.Trim()} => {bodyInput})");
                        }
                        break;
                    }
                    case "lambda: from logic":
                    {
                        // SPECIAL NODE: "Convert Logic to Lambda"
                        // Takes multiple inputs and wraps them in a lambda
                        // Parameter name comes from Logic field (LITERAL:<name>)
                        var paramName = ExtractLiteralValue(node.Logic) ?? "x";
                        
                        // Collect all input bodies and combine them
                        var inputNames = node.Inputs?.Select(i => i.Name).ToList() ?? new List<string>();
                        var bodyParts = new List<string>();
                        
                        foreach (var inpName in inputNames)
                        {
                            if (inputValues.TryGetValue(inpName, out var val) && !string.IsNullOrEmpty(val))
                            {
                                // If input is a lambda expression, extract its body
                                if (val.Contains("=>"))
                                {
                                    var idx = val.IndexOf("=>", StringComparison.Ordinal);
                                    bodyParts.Add(val.Substring(idx + 2).Trim());
                                }
                                else
                                {
                                    bodyParts.Add(val);
                                }
                            }
                        }
                        
                        // Generate combined lambda body
                        // If only one input, just use it. If multiple, combine with semicolons
                        string lambdaBody;
                        if (bodyParts.Count == 0)
                        {
                            lambdaBody = "true";
                        }
                        else if (bodyParts.Count == 1)
                        {
                            lambdaBody = bodyParts[0];
                        }
                        else
                        {
                            lambdaBody = $"({string.Join("; ", bodyParts)})";
                        }
                        
                        DeclareExpr(sb, declared, node, "Lambda", $"({paramName.Trim()} => {lambdaBody})");
                        break;
                    }

                    // Postgres: Advanced
                    case "pg: bulk insert":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var table = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        var cols  = Inp(inputValues, "Columns", "Array.Empty<string>()");
                        var rows  = Inp(inputValues, "Rows", "Array.Empty<object>()");
                        sb.AppendLine($"long __copied = 0;");
                        sb.AppendLine($"using (var __wr = {conn}.BeginBinaryImport($\"COPY \\\"{table}\\\" ({{string.Join(\",\", (string[])({cols}))}}) FROM STDIN (FORMAT BINARY)\"))");
                        sb.AppendLine($"{{");
                        sb.AppendLine($"    foreach (var __row in (System.Collections.IEnumerable)({rows}))");
                        sb.AppendLine($"    {{");
                        sb.AppendLine($"        __wr.StartRow();");
                        sb.AppendLine($"        foreach (var __c in (string[])({cols}))");
                        sb.AppendLine($"            __wr.Write(__row.GetType().GetProperty(__c)?.GetValue(__row));");
                        sb.AppendLine($"        __copied++;");
                        sb.AppendLine($"    }}");
                        sb.AppendLine($"    __wr.Complete();");
                        sb.AppendLine($"}}");
                        DeclareExpr(sb, declared, node, "Affected", "__copied");
                        break;
                    }
                    case "pg: prepare":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        var sql  = Inp(inputValues, "Sql", "\"\"");
                        sb.AppendLine($"var __cmd = new NpgsqlCommand({sql}, {conn});");
                        sb.AppendLine($"__cmd.Prepare();");
                        DeclareExpr(sb, declared, node, "Command", "__cmd",
                            customType: "NpgsqlCommand");
                        break;
                    }
                    case "pg: run prepared":
                    {
                        var cmd  = Inp(inputValues, "Command", "null");
                        var pars = inputValues.TryGetValue("Params", out var p) ? p : null;
                        if (pars != null)
                        {
                            sb.AppendLine($"{cmd}.Parameters.Clear();");
                            sb.AppendLine($"foreach (var __kv in (System.Collections.Generic.IDictionary<string, object>)({pars}))");
                            sb.AppendLine($"    {cmd}.Parameters.AddWithValue(__kv.Key, __kv.Value ?? System.DBNull.Value);");
                        }
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {cmd}.ExecuteNonQueryAsync()");
                        break;
                    }
                    case "pg: batch execute":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var stmts = Inp(inputValues, "Statements", "Array.Empty<string>()");
                        sb.AppendLine($"using var __batch = {conn}.CreateBatch();");
                        sb.AppendLine($"foreach (var __s in (string[])({stmts}))");
                        sb.AppendLine($"    __batch.BatchCommands.Add(new NpgsqlBatchCommand(__s));");
                        DeclareExpr(sb, declared, node, "Affected",
                            "await __batch.ExecuteNonQueryAsync()");
                        break;
                    }
                    case "pg: notify":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        var ch   = Inp(inputValues, "Channel", "\"\"");
                        var pay  = Inp(inputValues, "Payload", "\"\"");
                        sb.AppendLine($"using (var __nc = new NpgsqlCommand($\"NOTIFY {{{StripQuotesExpr(ch)}}}, '{{{StripQuotesExpr(pay)}}}'\", {conn})) await __nc.ExecuteNonQueryAsync();");
                        break;
                    }
                    case "pg: listen":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        var ch   = Inp(inputValues, "Channel", "\"\"");
                        var cb   = Inp(inputValues, "Callback", "(_, __) => {}");
                        sb.AppendLine($"using (var __lc = new NpgsqlCommand($\"LISTEN {{{StripQuotesExpr(ch)}}}\", {conn})) await __lc.ExecuteNonQueryAsync();");
                        sb.AppendLine($"{conn}.Notification += {cb};");
                        break;
                    }
                    case "concat":
                    {
                        var a = Inp(inputValues, "A", "string.Empty");
                        var b = Inp(inputValues, "B", "string.Empty");
                        DeclareExpr(sb, declared, node, "Result", $"string.Concat({a}, {b})");
                        break;
                    }
                    case "format":
                    {
                        var tpl = Inp(inputValues, "Template", "\"\"");
                        var arg = Inp(inputValues, "Arg0", "null");
                        DeclareExpr(sb, declared, node, "Result", $"string.Format({tpl}, {arg})");
                        break;
                    }
                    case "weburl":
                    {
                        var url = Inp(inputValues, "URL", "\"https://example.com\"");
                        DeclareExpr(sb, declared, node, "URL", $"new Uri({url})");
                        break;
                    }
                    case "custom":
                    {
                        var enabled = Inp(inputValues, "Enabled", "true");
                        var value = Inp(inputValues, "Value", "null");
                        DeclareExpr(sb, declared, node, "Value", value, customType: node.Outputs.FirstOrDefault()?.CustomTypeName);
                        break;
                    }
                    // EFC / Database nodes
                    case "ef: query all":
                    {
                        var entity = Inp(inputValues, "EntityType", "\"Entity\"");
                        DeclareExpr(sb, declared, node, "Results", $"await _context.Set<{StripQuotes(entity)}>().ToListAsync()");
                        break;
                    }
                    case "ef: query where":
                    {
                        var entity = Inp(inputValues, "EntityType", "\"Entity\"");
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareExpr(sb, declared, node, "Results", $"await _context.Set<{StripQuotes(entity)}>().Where({pred}).ToListAsync()");
                        break;
                    }
                    case "ef: find by id":
                    {
                        var entity = Inp(inputValues, "EntityType", "\"Entity\"");
                        var keyVal = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Result", $"await _context.Set<{StripQuotes(entity)}>().FindAsync({keyVal})");
                        break;
                    }
                    case "ef: insert":
                    {
                        var entity = Inp(inputValues, "Entity", "entity");
                        sb.AppendLine($"_context.Add({entity});");
                        sb.AppendLine($"await _context.SaveChangesAsync();");
                        break;
                    }
                    case "ef: update":
                    {
                        var entity = Inp(inputValues, "Entity", "entity");
                        sb.AppendLine($"_context.Update({entity});");
                        sb.AppendLine($"await _context.SaveChangesAsync();");
                        break;
                    }
                    case "ef: delete":
                    {
                        var entity = Inp(inputValues, "Entity", "entity");
                        sb.AppendLine($"_context.Remove({entity});");
                        sb.AppendLine($"await _context.SaveChangesAsync();");
                        break;
                    }
                    case "stdb: insert":
                    {
                        var entity = Inp(inputValues, "Entity", "entity");
                        sb.AppendLine($"{entity}.Insert();");
                        break;
                    }
                    case "stdb: delete":
                    {
                        var entity = Inp(inputValues, "Entity", "entity");
                        sb.AppendLine($"{entity}.Delete();");
                        break;
                    }
                    case "stdb: filter by id":
                    {
                        var entityType = Inp(inputValues, "EntityType", "\"Entity\"");
                        var idFilter = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Result", $"{StripQuotes(entityType)}.FilterById({idFilter}).FirstOrDefault()");
                        break;
                    }

                    // Literals — runtime value comes from the inline editor,
                    // stored as Logic = "LITERAL:<text>".
                    case "string literal":
                    {
                        var lit = ExtractLiteralValue(node.Logic) ?? "";
                        DeclareExpr(sb, declared, node, "Value", $"\"{EscapeString(lit)}\"");
                        break;
                    }
                    case "int literal":
                    {
                        var lit = ExtractLiteralValue(node.Logic) ?? "0";
                        DeclareExpr(sb, declared, node, "Value", lit);
                        break;
                    }
                    case "float literal":
                    {
                        var lit = ExtractLiteralValue(node.Logic) ?? "0";
                        if (!lit.EndsWith("f", StringComparison.OrdinalIgnoreCase) &&
                            !lit.EndsWith("d", StringComparison.OrdinalIgnoreCase) &&
                            !lit.EndsWith("m", StringComparison.OrdinalIgnoreCase))
                            lit += "d";
                        DeclareExpr(sb, declared, node, "Value", lit);
                        break;
                    }
                    case "bool literal":
                    {
                        var lit = (ExtractLiteralValue(node.Logic) ?? "false").Trim().ToLowerInvariant();
                        DeclareExpr(sb, declared, node, "Value", lit == "true" ? "true" : "false");
                        break;
                    }
                    case "null":
                    {
                        DeclareExpr(sb, declared, node, "Value", "null");
                        break;
                    }
                    case "json literal":
                    {
                        // Auto-detect: JSON-shaped value → JsonNode.Parse, else
                        // emit as a plain string. Lets users type either a
                        // raw string or a JSON object/array in the same field.
                        var lit = ExtractLiteralValue(node.Logic) ?? "";
                        if (LooksLikeJson(lit))
                        {
                            var verbatim = "@\"" + lit.Replace("\"", "\"\"") + "\"";
                            DeclareExpr(sb, declared, node, "Value",
                                $"System.Text.Json.Nodes.JsonNode.Parse({verbatim})");
                        }
                        else
                        {
                            DeclareExpr(sb, declared, node, "Value", $"\"{EscapeString(lit)}\"");
                        }
                        break;
                    }
                    case "connection string literal":
                    {
                        var lit = ExtractLiteralValue(node.Logic) ?? "";
                        DeclareExpr(sb, declared, node, "Value", $"\"{EscapeString(lit)}\"");
                        break;
                    }
                    case "predicate literal":
                    {
                        // Emit the lambda verbatim so users can write "x => x.IsActive".
                        var lit = ExtractLiteralValue(node.Logic) ?? "x => true";
                        DeclareExpr(sb, declared, node, "Value", lit);
                        break;
                    }
                    case "custom literal":
                    {
                        // Logic is "CUSTOMLIT::<TypeName>\n<body>". The body is
                        // auto-detected: JSON-shaped → Deserialize<T>(@"...");
                        // anything else → treat as a plain string literal of type T.
                        var raw = node.Logic ?? "";
                        const string prefix = "CUSTOMLIT::";
                        var body = raw.StartsWith(prefix, StringComparison.Ordinal)
                            ? raw.Substring(prefix.Length) : raw;
                        var nl = body.IndexOf('\n');
                        var clTypeName = (nl < 0 ? body : body.Substring(0, nl)).Trim();
                        var clBody     = nl < 0 ? "" : body.Substring(nl + 1);
                        if (string.IsNullOrEmpty(clTypeName)) clTypeName = "object";

                        string clExpr;
                        if (LooksLikeJson(clBody))
                        {
                            var clVerbatim = "@\"" + clBody.Replace("\"", "\"\"") + "\"";
                            clExpr = $"System.Text.Json.JsonSerializer.Deserialize<{clTypeName}>({clVerbatim})";
                        }
                        else if (clTypeName == "string" || clTypeName == "String" || clTypeName == "object")
                        {
                            clExpr = $"\"{EscapeString(clBody)}\"";
                        }
                        else
                        {
                            // Fall back to deserialising a JSON-encoded string —
                            // works for primitives like int/double/bool when the
                            // user typed e.g. `42` or `true`.
                            var quoted = "\"" + EscapeString(clBody) + "\"";
                            clExpr = $"System.Text.Json.JsonSerializer.Deserialize<{clTypeName}>({quoted})";
                        }
                        DeclareExpr(sb, declared, node, "Value", clExpr, customType: clTypeName);
                        break;
                    }
                    case "custom input":
                    {
                        // CUSTOMINPUT(<Type>) — the value travels in as a parameter to
                        // the generated function. Emit a passthrough so wires resolve.
                        var raw = node.Logic ?? "";
                        var m = System.Text.RegularExpressions.Regex.Match(
                            raw, @"^CUSTOMINPUT\(\s*([\w\.]+)\s*\)\s*$");
                        var typeName = m.Success ? m.Groups[1].Value : "object";
                        var paramName = SafeId(node.Outputs.FirstOrDefault()?.Name ?? "value");
                        DeclareExpr(sb, declared, node, "Value", paramName, customType: typeName);
                        break;
                    }

                    // EF Core: Easy — beginner-friendly nodes that read like English.
                    case "db: open":
                    {
                        var cs = Inp(inputValues, "ConnectionString", "\"\"");
                        DeclareExpr(sb, declared, node, "Db",
                            $"new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql({cs}).Options)",
                            customType: "AppDbContext");
                        break;
                    }
                    case "db: save":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        DeclareExpr(sb, declared, node, "Affected", $"await {db}.SaveChangesAsync()");
                        break;
                    }
                    case "db: close":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        sb.AppendLine($"await {db}.DisposeAsync();");
                        break;
                    }
                    case "db: get all":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().ToListAsync()");
                        break;
                    }
                    case "db: get one by id":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var idGOBI  = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Row",
                            $"await {db}.Set<{ent}>().FindAsync({idGOBI})");
                        break;
                    }
                    case "db: get where":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().Where({pred}).ToListAsync()");
                        break;
                    }
                    case "db: get first":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareExpr(sb, declared, node, "Row",
                            $"await {db}.Set<{ent}>().FirstOrDefaultAsync({pred})");
                        break;
                    }
                    case "db: count":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", null);
                        var expr = pred == null
                            ? $"await {db}.Set<{ent}>().CountAsync()"
                            : $"await {db}.Set<{ent}>().CountAsync({pred})";
                        DeclareExpr(sb, declared, node, "Count", expr);
                        break;
                    }
                    case "db: add":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var en = Inp(inputValues, "Entity", "null");
                        sb.AppendLine($"{db}.Add({en});");
                        break;
                    }
                    case "db: add and save":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var en = Inp(inputValues, "Entity", "null");
                        sb.AppendLine($"{db}.Add({en});");
                        DeclareExpr(sb, declared, node, "Affected", $"await {db}.SaveChangesAsync()");
                        break;
                    }
                    case "db: update and save":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var en = Inp(inputValues, "Entity", "null");
                        sb.AppendLine($"{db}.Update({en});");
                        DeclareExpr(sb, declared, node, "Affected", $"await {db}.SaveChangesAsync()");
                        break;
                    }
                    case "db: remove and save":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var en = Inp(inputValues, "Entity", "null");
                        sb.AppendLine($"{db}.Remove({en});");
                        DeclareExpr(sb, declared, node, "Affected", $"await {db}.SaveChangesAsync()");
                        break;
                    }
                    case "db: exists":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareExpr(sb, declared, node, "Exists",
                            $"await {db}.Set<{ent}>().AnyAsync({pred})");
                        break;
                    }
                    case "db: begin tx":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        DeclareExpr(sb, declared, node, "Tx",
                            $"await {db}.Database.BeginTransactionAsync()",
                            customType: "IDbContextTransaction");
                        break;
                    }
                    case "db: commit tx":
                    {
                        var tx = Inp(inputValues, "Tx", "null");
                        sb.AppendLine($"await {tx}.CommitAsync();");
                        break;
                    }
                    case "db: rollback tx":
                    {
                        var tx = Inp(inputValues, "Tx", "null");
                        sb.AppendLine($"await {tx}.RollbackAsync();");
                        break;
                    }

                    // ── Predicate builders ─────────────────────────────────
                    // Each emits NO local variable — they produce a lambda
                    // expression text that's stored in _predicateExpressions
                    // and inlined at the consumer's call site.
                    case "where: equals":
                    case "where: not equals":
                    case "where: greater":
                    case "where: less":
                    case "where: contains":
                    {
                        // Property name comes from the inspector / wired literal.
                        var prop = SanitiseTypeName(LiteralFromConnectedPort("Property")
                                                    ?? StripQuotesExpr(Inp(inputValues, "Property", "Property")));
                        var val = Inp(inputValues, "Value", "default");
                        string body = title switch
                        {
                            "where: equals"     => $"x.{prop} == {val}",
                            "where: not equals" => $"x.{prop} != {val}",
                            "where: greater"    => $"x.{prop} > {val}",
                            "where: less"       => $"x.{prop} < {val}",
                            "where: contains"   => $"x.{prop}.Contains({val})",
                            _                   => "true"
                        };
                        StorePredicate("Predicate", $"x => {body}");
                        break;
                    }
                    case "where: and":
                    case "where: or":
                    {
                        // The upstream predicates already arrived here as lambda
                        // text via the inputValues splat — pull the body off
                        // each so we can wrap them under a single `x =>`.
                        var a = Inp(inputValues, "A", "x => true");
                        var b = Inp(inputValues, "B", "x => true");
                        var op = title == "where: and" ? "&&" : "||";
                        StorePredicate("Predicate", $"x => ({LambdaBody(a)}) {op} ({LambdaBody(b)})");
                        break;
                    }

                    // Result-shaping helpers
                    case "db: order by":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var key  = Inp(inputValues, "KeySelector", "x => x");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().OrderBy({key}).ToListAsync()");
                        break;
                    }
                    case "db: order by desc":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var key  = Inp(inputValues, "KeySelector", "x => x");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().OrderByDescending({key}).ToListAsync()");
                        break;
                    }
                    case "db: page":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var skip = Inp(inputValues, "Skip", "0");
                        var take = Inp(inputValues, "Take", "50");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().Skip({skip}).Take({take}).ToListAsync()");
                        break;
                    }
                    case "db: include":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var nav  = Inp(inputValues, "Navigation", "x => x");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"await {db}.Set<{ent}>().Include({nav}).ToListAsync()");
                        break;
                    }

                    // Row-Level Security (Postgres). All emit raw SQL via
                    // Database.ExecuteSqlRawAsync — table/policy names are
                    // identifier-quoted; values use parameter binding where
                    // it works, else string.Format with NpgsqlDbCommand.
                    case "db: rls enable":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{{{t}}}\\\" ENABLE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls disable":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{{{t}}}\\\" DISABLE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls force":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{{{t}}}\\\" FORCE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls create policy":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var t    = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        var name = StripQuotesExpr(Inp(inputValues, "PolicyName", "\"policy\""));
                        var op   = StripQuotesExpr(Inp(inputValues, "Operation", "\"ALL\""));
                        var role = StripQuotesExpr(Inp(inputValues, "Role", "\"PUBLIC\""));
                        var usng = StripQuotesExpr(Inp(inputValues, "Using", "\"true\""));
                        // WithCheck is optional — emit the WITH CHECK clause only if present.
                        var checkVal = inputValues.TryGetValue("WithCheck", out var wc) ? wc : null;
                        var checkClause = string.IsNullOrEmpty(checkVal)
                            ? ""
                            : $" WITH CHECK ({{{StripQuotesExpr(checkVal)}}})";
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"CREATE POLICY \\\"{{{name}}}\\\" ON \\\"{{{t}}}\\\" FOR {{{op}}} TO {{{role}}} USING ({{{usng}}}){checkClause}\")");
                        break;
                    }
                    case "db: rls drop policy":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var t    = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        var name = StripQuotesExpr(Inp(inputValues, "PolicyName", "\"policy\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"DROP POLICY IF EXISTS \\\"{{{name}}}\\\" ON \\\"{{{t}}}\\\"\")");
                        break;
                    }
                    case "db: rls set user":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "\"\"");
                        // SET LOCAL must run inside a transaction; that's the user's responsibility.
                        sb.AppendLine($"await {db}.Database.ExecuteSqlRawAsync(\"SELECT set_config('app.current_user', {{0}}, true)\", {u});");
                        break;
                    }
                    case "db: rls reset user":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        sb.AppendLine($"await {db}.Database.ExecuteSqlRawAsync(\"RESET app.current_user\");");
                        break;
                    }
                    case "db: raw sql":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var sql  = Inp(inputValues, "Sql", "\"\"");
                        var pars = Inp(inputValues, "Params", null);
                        var expr = pars == null
                            ? $"await {db}.Database.ExecuteSqlRawAsync({sql})"
                            : $"await {db}.Database.ExecuteSqlRawAsync({sql}, (object[])({pars}))";
                        DeclareExpr(sb, declared, node, "Affected", expr);
                        break;
                    }

                    // SpacetimeDB: Easy
                    case "sdb: connect":
                    {
                        var uri    = Inp(inputValues, "Uri", "\"http://localhost:3000\"");
                        var module = Inp(inputValues, "Module", "\"my_module\"");
                        var token  = Inp(inputValues, "AuthToken", "null");
                        DeclareExpr(sb, declared, node, "Conn",
                            $"DbConnection.Builder().WithUri({uri}).WithModuleName({module}).WithToken({token}).Build()",
                            customType: "DbConnection");
                        break;
                    }
                    case "sdb: disconnect":
                    {
                        var c = Inp(inputValues, "Conn", "null");
                        sb.AppendLine($"{c}.Disconnect();");
                        break;
                    }
                    case "sdb: subscribe":
                    {
                        var c = Inp(inputValues, "Conn", "null");
                        var q = Inp(inputValues, "Queries", "new string[]{}");
                        sb.AppendLine($"{c}.SubscriptionBuilder().OnApplied(_ => {{ }}).Subscribe((string[])({q}));");
                        break;
                    }
                    case "sdb: call reducer":
                    {
                        var c = Inp(inputValues, "Conn", "null");
                        var r = Inp(inputValues, "Reducer", "\"\"");
                        var a = Inp(inputValues, "Args", "Array.Empty<object>()");
                        sb.AppendLine($"{c}.Reducers.Call({r}, (object[])({a}));");
                        break;
                    }
                    case "sdb: iter table":
                    {
                        var c = Inp(inputValues, "Conn", "null");
                        var t = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Rows",
                            $"{c}.Db.{t}.Iter().ToList()");
                        break;
                    }
                    case "sdb: find by pk":
                    {
                        var c  = Inp(inputValues, "Conn", "null");
                        var t  = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        var pk = Inp(inputValues, "Pk", "0");
                        DeclareExpr(sb, declared, node, "Row",
                            $"{c}.Db.{t}.FindByPrimaryKey({pk})");
                        break;
                    }
                    case "sdb: on insert":
                    case "sdb: on update":
                    case "sdb: on delete":
                    {
                        var c  = Inp(inputValues, "Conn", "null");
                        var t  = StripQuotesExpr(Inp(inputValues, "Table", "\"table\""));
                        var cb = Inp(inputValues, "Callback", "(_, __) => {}");
                        var hook = title.EndsWith("insert", StringComparison.Ordinal) ? "OnInsert"
                                 : title.EndsWith("update", StringComparison.Ordinal) ? "OnUpdate"
                                 : "OnDelete";
                        sb.AppendLine($"{c}.Db.{t}.{hook} += {cb};");
                        break;
                    }

                    // HTTP / HTTP/2. Every request is built with
                    // Version=HttpVersion.Version20, VersionPolicy=RequestVersionOrLower
                    // so the runtime tries HTTP/2 first and gracefully falls back.
                    case "http: new client":
                    {
                        DeclareExpr(sb, declared, node, "Client",
                            "new HttpClient(new SocketsHttpHandler { EnableMultipleHttp2Connections = true }) " +
                            "{ DefaultRequestVersion = HttpVersion.Version20, " +
                            "DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower }",
                            customType: "HttpClient");
                        break;
                    }
                    case "http: get":
                    case "http: delete":
                    {
                        var cli  = Inp(inputValues, "Client", "_http");
                        var url  = Inp(inputValues, "Url", "\"\"");
                        var verb = title == "http: get" ? "Get" : "Delete";
                        DeclareExpr(sb, declared, node, "Response",
                            $"await {cli}.SendAsync(new HttpRequestMessage(HttpMethod.{verb}, {url}) " +
                            $"{{ Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionOrLower }})",
                            customType: "HttpResponseMessage");
                        break;
                    }
                    case "http: post json":
                    case "http: put json":
                    {
                        var cli  = Inp(inputValues, "Client", "_http");
                        var url  = Inp(inputValues, "Url", "\"\"");
                        var body = Inp(inputValues, "Body", "null");
                        var verb = title == "http: post json" ? "Post" : "Put";
                        DeclareExpr(sb, declared, node, "Response",
                            $"await {cli}.SendAsync(new HttpRequestMessage(HttpMethod.{verb}, {url}) " +
                            $"{{ Version = HttpVersion.Version20, " +
                            $"VersionPolicy = HttpVersionPolicy.RequestVersionOrLower, " +
                            $"Content = JsonContent.Create({body}) }})",
                            customType: "HttpResponseMessage");
                        break;
                    }
                    case "http: send":
                    {
                        var cli = Inp(inputValues, "Client",  "_http");
                        var req = Inp(inputValues, "Request", "null");
                        DeclareExpr(sb, declared, node, "Response",
                            $"await {cli}.SendAsync({req})",
                            customType: "HttpResponseMessage");
                        break;
                    }
                    case "http: read json":
                    {
                        var resp = Inp(inputValues, "Response", "null");
                        // Inspector-typed target type. The Custom Literal pattern
                        // doesn't apply here — we read it from the output port's
                        // CustomTypeName so the user sets it via the inspector.
                        var output = node.Outputs.FirstOrDefault();
                        var targetType = !string.IsNullOrEmpty(output?.CustomTypeName)
                            ? output.CustomTypeName : "object";
                        DeclareExpr(sb, declared, node, "Value",
                            $"await {resp}.Content.ReadFromJsonAsync<{targetType}>()",
                            customType: targetType);
                        break;
                    }
                    case "http: read string":
                    {
                        var resp = Inp(inputValues, "Response", "null");
                        DeclareExpr(sb, declared, node, "Body",
                            $"await {resp}.Content.ReadAsStringAsync()");
                        break;
                    }
                    case "http: status code":
                    {
                        var resp = Inp(inputValues, "Response", "null");
                        DeclareExpr(sb, declared, node, "Code", $"(int){resp}.StatusCode");
                        break;
                    }
                    case "http: set bearer token":
                    {
                        var cli = Inp(inputValues, "Client", "_http");
                        var tok = Inp(inputValues, "Token", "\"\"");
                        sb.AppendLine($"{cli}.DefaultRequestHeaders.Authorization = " +
                                      $"new AuthenticationHeaderValue(\"Bearer\", {tok});");
                        break;
                    }
                    case "http: set header":
                    {
                        var cli  = Inp(inputValues, "Client", "_http");
                        var name = Inp(inputValues, "Name",   "\"\"");
                        var val  = Inp(inputValues, "Value",  "\"\"");
                        sb.AppendLine($"{cli}.DefaultRequestHeaders.Remove({name});");
                        sb.AppendLine($"{cli}.DefaultRequestHeaders.Add({name}, {val});");
                        break;
                    }
                    case "http: ensure success":
                    {
                        var resp = Inp(inputValues, "Response", "null");
                        sb.AppendLine($"{resp}.EnsureSuccessStatusCode();");
                        break;
                    }

                    // Object / Type helpers
                    case "cast: to type":
                    {
                        var v = Inp(inputValues, "Value", "null");
                        var output = node.Outputs.FirstOrDefault();
                        var targetType = !string.IsNullOrEmpty(output?.CustomTypeName)
                            ? output.CustomTypeName : "object";
                        DeclareExpr(sb, declared, node, "Result",
                            $"({targetType})({v})",
                            customType: targetType);
                        break;
                    }

                    // Postgres / Npgsql — Dapper-style call sites. The compiler
                    // adds Npgsql + Dapper to the using set on demand below.
                    case "pg: connect":
                    {
                        var cs = Inp(inputValues, "ConnectionString", "\"\"");
                        DeclareExpr(sb, declared, node, "Connection",
                            $"new Npgsql.NpgsqlConnection({cs})");
                        var connVar = NodeVarName(node, "Connection");
                        sb.AppendLine($"{connVar}.Open();");
                        break;
                    }
                    case "pg: query":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var sql   = Inp(inputValues, "Sql", "\"\"");
                        var pars  = Inp(inputValues, "Params", "null");
                        DeclareExpr(sb, declared, node, "Rows",
                            $"Dapper.SqlMapper.Query({conn}, {sql}, {pars}).ToList()");
                        break;
                    }
                    case "pg: query first":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var sql   = Inp(inputValues, "Sql", "\"\"");
                        var pars  = Inp(inputValues, "Params", "null");
                        DeclareExpr(sb, declared, node, "Row",
                            $"Dapper.SqlMapper.QueryFirstOrDefault({conn}, {sql}, {pars})");
                        break;
                    }
                    case "pg: execute":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        var sql  = Inp(inputValues, "Sql", "\"\"");
                        var pars = Inp(inputValues, "Params", "null");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"Dapper.SqlMapper.Execute({conn}, {sql}, {pars})");
                        break;
                    }
                    case "pg: insert":
                    {
                        var conn   = Inp(inputValues, "Connection", "null");
                        var table  = Inp(inputValues, "Table", "\"table\"");
                        var entity = Inp(inputValues, "Entity", "null");
                        DeclareExpr(sb, declared, node, "Id",
                            $"Dapper.SqlMapper.ExecuteScalar({conn}, " +
                            $"$\"INSERT INTO {{{StripQuotesExpr(table)}}} ({{string.Join(\",\", {entity}.GetType().GetProperties().Select(p => p.Name))}}) \" + " +
                            $"$\"VALUES ({{string.Join(\",\", {entity}.GetType().GetProperties().Select(p => \"@\" + p.Name))}}) RETURNING id\", {entity})");
                        break;
                    }
                    case "pg: update by id":
                    {
                        var conn   = Inp(inputValues, "Connection", "null");
                        var table  = Inp(inputValues, "Table", "\"table\"");
                        var entity = Inp(inputValues, "Entity", "null");
                        var idUPG     = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"Dapper.SqlMapper.Execute({conn}, " +
                            $"$\"UPDATE {{{StripQuotesExpr(table)}}} SET {{string.Join(\",\", {entity}.GetType().GetProperties().Where(p => p.Name != \\\"Id\\\").Select(p => p.Name + \\\"=@\\\" + p.Name))}} WHERE id = @__id\", " +
                            $"new {{ Entity = {entity}, __id = {idUPG} }})");
                        break;
                    }
                    case "pg: delete by id":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var table = Inp(inputValues, "Table", "\"table\"");
                        var idDPG    = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"Dapper.SqlMapper.Execute({conn}, $\"DELETE FROM {{{StripQuotesExpr(table)}}} WHERE id = @id\", new {{ id = {idDPG} }})");
                        break;
                    }
                    case "pg: count":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var table = Inp(inputValues, "Table", "\"table\"");
                        var where = Inp(inputValues, "Where", "\"1=1\"");
                        var pars  = Inp(inputValues, "Params", "null");
                        DeclareExpr(sb, declared, node, "Count",
                            $"Dapper.SqlMapper.ExecuteScalar<long>({conn}, $\"SELECT COUNT(*) FROM {{{StripQuotesExpr(table)}}} WHERE {{{StripQuotesExpr(where)}}}\", {pars})");
                        break;
                    }
                    case "pg: begin tx":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        DeclareExpr(sb, declared, node, "Transaction", $"{conn}.BeginTransaction()");
                        break;
                    }
                    case "pg: commit tx":
                    {
                        var tx = Inp(inputValues, "Transaction", "null");
                        sb.AppendLine($"{tx}.Commit();");
                        break;
                    }
                    case "pg: rollback tx":
                    {
                        var tx = Inp(inputValues, "Transaction", "null");
                        sb.AppendLine($"{tx}.Rollback();");
                        break;
                    }
                    case "pg: close":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        sb.AppendLine($"{conn}.Close();");
                        sb.AppendLine($"{conn}.Dispose();");
                        break;
                    }

                    // Pariah-backed Auth nodes. The generator assumes a single
                    // shared `_auth` Pariah_Cybersecurity.DataHandler.DataRequest
                    // instance; declare it once at field scope in the host class.
                    case "auth: setup":
                    {
                        var dir = Inp(inputValues, "Directory", "\".\"");
                        sb.AppendLine($"await _auth.SetupFiles({dir});");
                        break;
                    }
                    case "auth: sign up":
                    {
                        var u   = Inp(inputValues, "Username", "\"\"");
                        var p   = Inp(inputValues, "Password", "\"\"");
                        var dir = Inp(inputValues, "Directory", "\".\"");
                        DeclareExpr(sb, declared, node, "RecoveryKey",
                            $"await _auth.CreateUser({u}, new SecureData({p}), {dir})",
                            customType: "SecureData");
                        break;
                    }
                    case "auth: login":
                    {
                        var u   = Inp(inputValues, "Username", "\"\"");
                        var p   = Inp(inputValues, "Password", "\"\"");
                        var dir = Inp(inputValues, "Directory", "\".\"");
                        var tr  = Inp(inputValues, "Trusted", "false");
                        // Two-output node — use a tuple deconstruction.
                        var dk = NodeVarName(node, "DecryptKey");
                        var ses = NodeVarName(node, "Session");
                        if (declared.Add(dk) && declared.Add(ses))
                            sb.AppendLine($"var ({dk}, {ses}) = await _auth.LoginUser({u}, {dir}, new SecureData({p}), {tr});");
                        else
                            sb.AppendLine($"({dk}, {ses}) = await _auth.LoginUser({u}, {dir}, new SecureData({p}), {tr});");
                        break;
                    }
                    case "auth: validate session":
                    {
                        var s = Inp(inputValues, "Session", "null");
                        var k = Inp(inputValues, "DecryptKey", "null");
                        DeclareExpr(sb, declared, node, "Valid",
                            $"await _auth.ValidateSession({s}, {k})");
                        break;
                    }
                    case "auth: logout":
                    {
                        var s = Inp(inputValues, "Session", "null");
                        var k = Inp(inputValues, "DecryptKey", "null");
                        sb.AppendLine($"await _auth.LogoutUser({s}, {k});");
                        break;
                    }
                    case "auth: reset password":
                    {
                        var s  = Inp(inputValues, "Session", "null");
                        var k  = Inp(inputValues, "DecryptKey", "null");
                        var np = Inp(inputValues, "NewPassword", "\"\"");
                        var rk = Inp(inputValues, "RecoveryKey", "null");
                        sb.AppendLine($"await _auth.ResetPassword({s}, {k}, new SecureData({np}), {rk});");
                        break;
                    }
                    case "auth: hash password":
                    {
                        var p = Inp(inputValues, "Password", "\"\"");
                        DeclareExpr(sb, declared, node, "Hash",
                            $"await PasswordHandler.GeneratePasswordHashAsync(new SecureData({p}))",
                            customType: "PasswordCheckData");
                        break;
                    }
                    case "auth: verify password":
                    {
                        var p = Inp(inputValues, "Password", "\"\"");
                        var h = Inp(inputValues, "Hash", "default");
                        DeclareExpr(sb, declared, node, "Valid",
                            $"await PasswordHandler.ValidatePasswordAsync(new SecureData({p}), {h})");
                        break;
                    }
                    case "auth: generate password":
                    {
                        var len = Inp(inputValues, "Length", "16");
                        var lo  = Inp(inputValues, "Lowercase", "true");
                        var up  = Inp(inputValues, "Uppercase", "true");
                        var di  = Inp(inputValues, "Digits", "true");
                        var sy  = Inp(inputValues, "Symbols", "true");
                        DeclareExpr(sb, declared, node, "Password",
                            $"PasswordGenerator.GeneratePassword({len}, {lo}, {up}, {di}, {sy})");
                        break;
                    }
                    case "auth: list users":
                    {
                        var s = Inp(inputValues, "Session", "null");
                        var k = Inp(inputValues, "DecryptKey", "null");
                        DeclareExpr(sb, declared, node, "Usernames",
                            $"await _auth.GetAllUsernames({s}, {k})");
                        break;
                    }
                    case "auth: remove account":
                    {
                        var s = Inp(inputValues, "Session", "null");
                        var k = Inp(inputValues, "DecryptKey", "null");
                        sb.AppendLine($"await _auth.RemoveAccount({s}, {k});");
                        break;
                    }

                    // SSO — same _auth instance, but using the system-level
                    // helpers in DataRequest.
                    case "sso: create system":
                    {
                        var u   = Inp(inputValues, "Username", "\"\"");
                        var idr = Inp(inputValues, "Identifier", "null");
                        var p   = Inp(inputValues, "Password", "null");
                        var sw  = Inp(inputValues, "Software", "\"\"");
                        var au  = Inp(inputValues, "Author", "\"\"");
                        var ex  = Inp(inputValues, "ExePath", "\"\"");
                        var sp  = Inp(inputValues, "ServiceParent", "\"\"");
                        var ti  = Inp(inputValues, "Tiers", "1");
                        var pk  = Inp(inputValues, "PublicKey", "null");
                        var ui  = Inp(inputValues, "UserId", "null");
                        DeclareExpr(sb, declared, node, "AppKey",
                            $"await _auth.CreateNewSystem({u}, {idr}, {p}, {sw}, {au}, {ex}, {sp}, {ti}, {pk}, {ui})",
                            customType: "SecureData");
                        break;
                    }
                    case "sso: connect app":
                    {
                        var u   = Inp(inputValues, "Username", "\"\"");
                        var p   = Inp(inputValues, "Password", "null");
                        var dir = Inp(inputValues, "Directory", "\".\"");
                        var ti  = Inp(inputValues, "Tier", "\"User\"");
                        var pk  = Inp(inputValues, "PublicKey", "null");
                        DeclareExpr(sb, declared, node, "AppKey",
                            $"await _auth.CreateNewApp({u}, {p}, {dir}, _ssoPaths, {ti}, {pk})",
                            customType: "SecureData");
                        break;
                    }
                    case "sso: verify session integrity":
                    {
                        var s   = Inp(inputValues, "Session", "null");
                        var msp = Inp(inputValues, "MainServicePath", "\"\"");
                        var pk  = Inp(inputValues, "PublicKey", "null");
                        sb.AppendLine($"await _auth.VerifySessionIntegrity(_ssoPaths, {s}, {msp}, {pk});");
                        break;
                    }
                    case "sso: get paths":
                    {
                        var idr = Inp(inputValues, "Identifier", "null");
                        var sw  = Inp(inputValues, "Software", "\"\"");
                        var au  = Inp(inputValues, "Author", "\"\"");
                        var pr  = Inp(inputValues, "Program", "\"\"");
                        var sp  = Inp(inputValues, "ServiceParent", "\"\"");
                        DeclareExpr(sb, declared, node, "Paths",
                            $"await _auth.GetPaths({idr}, {sw}, {au}, {pr}, {sp})",
                            customType: "DataRequest.DirectoryData");
                        break;
                    }
                    case "sso: add blacklist":
                    {
                        var sw  = Inp(inputValues, "Software", "\"\"");
                        var s   = Inp(inputValues, "Session", "null");
                        var msp = Inp(inputValues, "MainServicePath", "\"\"");
                        var pk  = Inp(inputValues, "PublicKey", "null");
                        sb.AppendLine($"await _auth.AddToBlacklist({sw}, {s}, {msp}, {pk});");
                        break;
                    }
                    case "sso: remove blacklist":
                    {
                        var sw  = Inp(inputValues, "Software", "\"\"");
                        var s   = Inp(inputValues, "Session", "null");
                        var msp = Inp(inputValues, "MainServicePath", "\"\"");
                        var pk  = Inp(inputValues, "PublicKey", "null");
                        sb.AppendLine($"await _auth.RemoveFromBlacklist({sw}, {s}, {msp}, {pk});");
                        break;
                    }
                    case "sso: device master secret":
                    {
                        var u = Inp(inputValues, "UserId", "\"\"");
                        DeclareExpr(sb, declared, node, "Secret",
                            $"DataHandler.DeviceIdentifier.GetUserBoundMasterSecret({u})",
                            customType: "SecureData");
                        break;
                    }

                    // Marketplace nodes. Generator assumes EF Core entities:
                    // Listing { Id, SellerId, ItemId, PriceCents, Title, Status, CreatedAt },
                    // Wallet { UserId (PK), BalanceCents },
                    // MarketTransaction { Id, ListingId, BuyerId, SellerId, PriceCents, At },
                    // Item { Id, OwnerId, Name }.
                    case "market: create listing":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var s  = Inp(inputValues, "SellerId", "0");
                        var i  = Inp(inputValues, "ItemId", "0");
                        var p  = Inp(inputValues, "PriceCents", "0");
                        var t  = Inp(inputValues, "Title", "\"\"");
                        sb.AppendLine($"var __listing = new Listing {{ SellerId = {s}, ItemId = {i}, PriceCents = {p}, Title = {t}, Status = \"open\", CreatedAt = DateTime.UtcNow }};");
                        sb.AppendLine($"{db}.Listings.Add(__listing);");
                        sb.AppendLine($"await {db}.SaveChangesAsync();");
                        DeclareExpr(sb, declared, node, "Listing", "__listing", customType: "Listing");
                        break;
                    }
                    case "market: cancel listing":
                    {
                        var db  = Inp(inputValues, "Db", "_db");
                        var lid = Inp(inputValues, "ListingId", "0");
                        var c   = Inp(inputValues, "CallerId", "0");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Listings.Where(l => l.Id == {lid} && l.SellerId == {c} && l.Status == \"open\").ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, \"cancelled\"))");
                        break;
                    }
                    case "market: buy listing":
                    {
                        var db  = Inp(inputValues, "Db", "_db");
                        var lid = Inp(inputValues, "ListingId", "0");
                        var b   = Inp(inputValues, "BuyerId", "0");
                        sb.AppendLine($"using var __tx = await {db}.Database.BeginTransactionAsync();");
                        sb.AppendLine($"var __l = await {db}.Listings.FirstOrDefaultAsync(l => l.Id == {lid} && l.Status == \"open\")");
                        sb.AppendLine($"    ?? throw new InvalidOperationException(\"Listing not available\");");
                        sb.AppendLine($"var __bw = await {db}.Wallets.FirstOrDefaultAsync(w => w.UserId == {b})");
                        sb.AppendLine($"    ?? throw new InvalidOperationException(\"Buyer wallet missing\");");
                        sb.AppendLine($"if (__bw.BalanceCents < __l.PriceCents) throw new InvalidOperationException(\"Insufficient funds\");");
                        sb.AppendLine($"var __sw = await {db}.Wallets.FirstOrDefaultAsync(w => w.UserId == __l.SellerId)");
                        sb.AppendLine($"    ?? new Wallet {{ UserId = __l.SellerId, BalanceCents = 0 }};");
                        sb.AppendLine($"if (__sw.UserId == __l.SellerId && {db}.Entry(__sw).State == EntityState.Detached) {db}.Wallets.Add(__sw);");
                        sb.AppendLine($"__bw.BalanceCents -= __l.PriceCents;");
                        sb.AppendLine($"__sw.BalanceCents += __l.PriceCents;");
                        sb.AppendLine($"__l.Status = \"sold\";");
                        sb.AppendLine($"var __it = await {db}.Items.FirstOrDefaultAsync(i => i.Id == __l.ItemId);");
                        sb.AppendLine($"if (__it != null) __it.OwnerId = {b};");
                        sb.AppendLine($"var __mt = new MarketTransaction {{ ListingId = __l.Id, BuyerId = {b}, SellerId = __l.SellerId, PriceCents = __l.PriceCents, At = DateTime.UtcNow }};");
                        sb.AppendLine($"{db}.MarketTransactions.Add(__mt);");
                        sb.AppendLine($"await {db}.SaveChangesAsync();");
                        sb.AppendLine($"await __tx.CommitAsync();");
                        DeclareExpr(sb, declared, node, "Transaction", "__mt", customType: "MarketTransaction");
                        break;
                    }
                    case "market: search listings":
                    {
                        var db    = Inp(inputValues, "Db", "_db");
                        var q     = Inp(inputValues, "TitleQuery", "null");
                        var minP  = Inp(inputValues, "MinPrice", "0");
                        var maxP  = Inp(inputValues, "MaxPrice", "long.MaxValue");
                        var skip  = Inp(inputValues, "Skip", "0");
                        var take  = Inp(inputValues, "Take", "50");
                        DeclareExpr(sb, declared, node, "Results",
                            $"await {db}.Listings.Where(l => l.Status == \"open\" && (string.IsNullOrEmpty({q}) || l.Title.Contains({q})) && l.PriceCents >= {minP} && l.PriceCents <= {maxP}).OrderByDescending(l => l.CreatedAt).Skip({skip}).Take({take}).ToListAsync()");
                        break;
                    }
                    case "market: get user listings":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var s  = Inp(inputValues, "SellerId", "0");
                        DeclareExpr(sb, declared, node, "Results",
                            $"await {db}.Listings.Where(l => l.SellerId == {s}).OrderByDescending(l => l.CreatedAt).ToListAsync()");
                        break;
                    }
                    case "market: get wallet":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "0");
                        sb.AppendLine($"var __w = await {db}.Wallets.FirstOrDefaultAsync(w => w.UserId == {u});");
                        sb.AppendLine($"if (__w == null) {{ __w = new Wallet {{ UserId = {u}, BalanceCents = 0 }}; {db}.Wallets.Add(__w); await {db}.SaveChangesAsync(); }}");
                        DeclareExpr(sb, declared, node, "BalanceCents", "__w.BalanceCents");
                        break;
                    }
                    case "market: add funds":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "0");
                        var c  = Inp(inputValues, "Cents", "0");
                        sb.AppendLine($"var __w = await {db}.Wallets.FirstOrDefaultAsync(w => w.UserId == {u})");
                        sb.AppendLine($"    ?? new Wallet {{ UserId = {u}, BalanceCents = 0 }};");
                        sb.AppendLine($"if ({db}.Entry(__w).State == EntityState.Detached) {db}.Wallets.Add(__w);");
                        sb.AppendLine($"__w.BalanceCents += {c};");
                        sb.AppendLine($"await {db}.SaveChangesAsync();");
                        DeclareExpr(sb, declared, node, "BalanceCents", "__w.BalanceCents");
                        break;
                    }
                    case "market: withdraw funds":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "0");
                        var c  = Inp(inputValues, "Cents", "0");
                        sb.AppendLine($"var __w = await {db}.Wallets.FirstOrDefaultAsync(w => w.UserId == {u})");
                        sb.AppendLine($"    ?? throw new InvalidOperationException(\"Wallet missing\");");
                        sb.AppendLine($"if (__w.BalanceCents < {c}) throw new InvalidOperationException(\"Insufficient funds\");");
                        sb.AppendLine($"__w.BalanceCents -= {c};");
                        sb.AppendLine($"await {db}.SaveChangesAsync();");
                        DeclareExpr(sb, declared, node, "BalanceCents", "__w.BalanceCents");
                        break;
                    }
                    case "market: get inventory":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "0");
                        DeclareExpr(sb, declared, node, "Items",
                            $"await {db}.Items.Where(i => i.OwnerId == {u}).ToListAsync()");
                        break;
                    }
                    case "market: transfer item":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var iid  = Inp(inputValues, "ItemId", "0");
                        var from = Inp(inputValues, "FromUser", "0");
                        var to   = Inp(inputValues, "ToUser", "0");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Items.Where(i => i.Id == {iid} && i.OwnerId == {from}).ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, {to}))");
                        break;
                    }
                    default:
                    {
                        // Entity-import nodes: Logic is "ENTITY:<TypeName>:<hash>".
                        // Emit `new TypeName { Prop1 = inA, Prop2 = inB, … }`
                        // and register the entity's CLR type for the output.
                        if (node.Logic != null && node.Logic.StartsWith("ENTITY:", StringComparison.Ordinal))
                        {
                            var entityName = node.Logic.Substring("ENTITY:".Length).Split(':').FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(entityName))
                            {
                                var inits = string.Join(", ", node.Inputs.Select(i =>
                                    $"{SafeId(i.Name)} = {Inp(inputValues, i.Name, DefaultLiteralFor(i.SemanticType, i.CustomTypeName))}"));
                                var output = node.Outputs.FirstOrDefault();
                                if (output != null)
                                    DeclareExpr(sb, declared, node, output.Name,
                                        $"new {entityName} {{ {inits} }}", customType: entityName);
                                else
                                    sb.AppendLine($"_ = new {entityName} {{ {inits} }};");
                                break;
                            }
                        }

                        // Heuristic: if Logic is empty / a placeholder comment
                        // (everything the script parser produces falls into
                        // this bucket — "// MyMethod — implement here" /
                        // "// MyMethod" / "// Foo property"), generate an
                        // actual call out of the node's title and ports
                        // instead of dumping the placeholder. This is what
                        // the user actually wants the "Custom Nodes" entries
                        // to compile to.
                        bool isPlaceholderLogic =
                               string.IsNullOrWhiteSpace(node.Logic)
                            || (node.Logic.TrimStart().StartsWith("//")
                                && !node.Logic.Contains('\n')
                                && !node.Logic.Contains(';')
                                && !node.Logic.Contains('{'));

                        if (!isPlaceholderLogic)
                        {
                            // User wrote real code in Logic — emit verbatim.
                            sb.AppendLine(node.Logic.Trim());
                            break;
                        }

                        // Build the argument list from connected inputs, with
                        // sensible per-type defaults for unconnected ones.
                        var args = string.Join(", ", node.Inputs.Select(i =>
                            Inp(inputValues, i.Name, DefaultLiteralFor(i.SemanticType, i.CustomTypeName))));

                        var callTarget = SafeId(node.Title ?? "Unknown");
                        bool propertyShape =
                               node.Inputs.Count == 0
                            && node.Outputs.Count == 1
                            && (node.Description?.StartsWith("Property:") == true);

                        bool nodeIsAsync = node.IsAsync;

                        if (propertyShape)
                        {
                            // Treat title as a property/field access.
                            var output = node.Outputs.First();
                            DeclareExpr(sb, declared, node, output.Name, callTarget);
                        }
                        else if (node.Outputs.Count == 0)
                        {
                            // void / fire-and-forget call.
                            var call = $"{callTarget}({args})";
                            sb.AppendLine(nodeIsAsync ? $"await {call};" : $"{call};");
                        }
                        else if (node.Outputs.Count == 1)
                        {
                            var output = node.Outputs.First();
                            var call = $"{callTarget}({args})";
                            DeclareExpr(sb, declared, node, output.Name,
                                nodeIsAsync ? $"await {call}" : call);
                        }
                        else
                        {
                            // Multi-output: emit a tuple deconstruction.
                            var lhs = string.Join(", ", node.Outputs.Select(o =>
                                $"var {NodeVarName(node, o.Name)}"));
                            var call = $"{callTarget}({args})";
                            sb.AppendLine($"({lhs}) = {(nodeIsAsync ? $"await {call}" : call)};");
                            foreach (var o in node.Outputs) declared.Add(NodeVarName(node, o.Name));
                        }
                        break;
                    }
                }

                return sb.ToString();
            }

            // Helpers

            private static void Declare(StringBuilder sb, HashSet<string> declared, string varName, string typeName, string value)
            {
                if (declared.Add(varName))
                    sb.AppendLine($"{typeName} {varName} = {value};");
                else
                    sb.AppendLine($"{varName} = {value};");
            }

            private static void DeclareExpr(StringBuilder sb, HashSet<string> declared, BareNode node, string portName, string expr, string customType = null)
            {
                var varName = NodeVarName(node, portName);
                var output = node.Outputs.FirstOrDefault(o => o.Name == portName);
                var typeName = output != null
                    ? SemanticTypeToCSharp(output.SemanticType, customType ?? output.CustomTypeName)
                    : "var";
                Declare(sb, declared, varName, typeName, expr);
            }

            private static string Inp(Dictionary<string, string> map, string key, string @default)
                => map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : @default;

            // Per-node generation context: upstream port types keyed by this
            // node's input port name. Set by EmitMainMethod before each
            // GenerateNodeCode call. Used so e.g. DB: Get All can read its
            // EntityType from a connected Custom Literal/Cast port instead of
            // parsing a string literal.
            [ThreadStatic] private static Dictionary<string, string> _inputTypeContext;
            [ThreadStatic] private static Dictionary<string, string> _inputLiteralContext;
            [ThreadStatic] private static Dictionary<string, string> _inputUpstreamTitle;
            [ThreadStatic] private static Dictionary<string, string> _predicateExpressions;
            [ThreadStatic] private static string _currentNodeUuid;

            private static string LiteralFromConnectedPort(string portName) =>
                _inputLiteralContext != null && _inputLiteralContext.TryGetValue(portName, out var t)
                    ? t : null;
            private static string UpstreamTitleOf(string portName) =>
                _inputUpstreamTitle != null && _inputUpstreamTitle.TryGetValue(portName, out var t)
                    ? t : null;
            private static void StorePredicate(string port, string lambda)
            {
                if (_predicateExpressions == null || string.IsNullOrEmpty(_currentNodeUuid)) return;
                _predicateExpressions[$"{_currentNodeUuid}:{port}"] = lambda;
            }
            // Pull `x => …body…` apart, returning just the body so multiple
            // predicates can be combined under a single lambda parameter.
            private static string LambdaBody(string lambda)
            {
                if (string.IsNullOrEmpty(lambda)) return "true";
                var i = lambda.IndexOf("=>", StringComparison.Ordinal);
                return i < 0 ? lambda : lambda.Substring(i + 2).Trim();
            }

            private static string TypeFromConnectedPort(string portName) =>
                _inputTypeContext != null && _inputTypeContext.TryGetValue(portName, out var t)
                    ? t : null;

            // Resolve the entity-type name for a DB/PG node. Priority:
            //   1. Wired upstream port carries a CustomTypeName ("Game") → use it.
            //   2. Upstream is a String/Custom Literal → sanitise the raw text
            //      (skip comments, accept "class Foo" → "Foo").
            //   3. Inline string literal value passed via inputValues.
            private static string ResolveEntityType(Dictionary<string, string> inputValues, string fallback = "Entity")
            {
                var wired = TypeFromConnectedPort("EntityType");
                if (!string.IsNullOrEmpty(wired)) return wired;
                var lit = LiteralFromConnectedPort("EntityType");
                if (!string.IsNullOrWhiteSpace(lit)) return SanitiseTypeName(lit);
                if (inputValues.TryGetValue("EntityType", out var v) && !string.IsNullOrWhiteSpace(v))
                    return SanitiseTypeName(StripQuotesExpr(v));
                return fallback;
            }

            // Strip whitespace/quotes/comments and return the first identifier-shaped
            // token. Lets users type "Game", "\"Game\"", or even paste a class
            // declaration whose useful line is `class Game`.
            private static string SanitiseTypeName(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "Entity";
                foreach (var raw in s.Split('\n'))
                {
                    var line = raw.Trim().Trim('"');
                    if (line.Length == 0) continue;
                    if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("/*", StringComparison.Ordinal)) continue;
                    var m = System.Text.RegularExpressions.Regex.Match(line,
                        @"\b(?:public\s+|private\s+|internal\s+|protected\s+|static\s+|sealed\s+|abstract\s+|partial\s+)*class\s+(\w+)");
                    if (m.Success) return m.Groups[1].Value;
                    var m2 = System.Text.RegularExpressions.Regex.Match(line, @"[A-Za-z_][\w\.]*");
                    if (m2.Success) return m2.Value;
                }
                return "Entity";
            }

            // Pull the user-typed value out of a literal node's Logic.
            // The inline editor stores it as "LITERAL:<text>".
            private static string ExtractLiteralValue(string logic) =>
                !string.IsNullOrEmpty(logic) && logic.StartsWith("LITERAL:", StringComparison.Ordinal)
                    ? logic.Substring("LITERAL:".Length)
                    : null;

            // String → "escaped" form safe to drop inside a "..." literal.
            private static string EscapeString(string s) =>
                (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");

            // Heuristic: does the trimmed value look like a JSON object/array/
            // scalar literal? Used by literal nodes to auto-detect content type.
            private static bool LooksLikeJson(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                var t = s.Trim();
                if (t.Length == 0) return false;
                char c = t[0];
                if (c == '{' || c == '[' || c == '"') return true;
                if (t == "null" || t == "true" || t == "false") return true;
                if ((c >= '0' && c <= '9') || c == '-')
                {
                    // pure number
                    return double.TryParse(t, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _);
                }
                return false;
            }

            // Emits an inline expression that coerces any value to a bool.
            // Used by logic gates so they accept `object` instead of strict
            // `bool`. The branch order matches what users expect from JS-style
            // truthiness, but stays type-safe.
            private static string TruthyExpr(string raw) =>
                $"({raw}) switch {{ null => false, bool __b => __b, string __s => !string.IsNullOrEmpty(__s), " +
                $"int __i => __i != 0, long __l => __l != 0, double __d => __d != 0, decimal __m => __m != 0, _ => true }}";

            // For interpolated SQL: if the input arrived as a quoted string
            // ("table"), unwrap it so we don't render `"table"` inside the
            // generated $"…" literal. If it's already a variable reference,
            // leave it.
            private static string StripQuotesExpr(string s)
            {
                if (string.IsNullOrEmpty(s)) return s;
                var t = s.Trim();
                if (t.Length >= 2 && t[0] == '"' && t[^1] == '"') return t.Substring(1, t.Length - 2);
                return s;
            }

            /// <summary>
            /// Reasonable default C# literal for an unconnected input port.
            /// Used by the custom/unknown-node code path so generated calls
            /// remain compilable even with required inputs left dangling.
            /// </summary>
            private static string DefaultLiteralFor(string semantic, string customTypeName = null) =>
                (semantic ?? "object").ToLowerInvariant() switch
                {
                    "number" => "0d",
                    "int"    => "0",
                    "string" => "string.Empty",
                    "bool"   => "false",
                    "weburl" => "null",
                    "custom" => string.IsNullOrEmpty(customTypeName) ? "null" : $"default({customTypeName})",
                    _        => "default"
                };

            public static string NodeVarName(BareNode node, string portName)
                => $"{SafeId(node.Title)}_{NodeShortId(node)}_{SafeId(portName)}";

            private static string NodeShortId(BareNode node)
                => (node.UUID?.Length >= 8 ? node.UUID.Substring(0, 8) : node.UUID ?? "unk").Replace("-", "");

            public static string SafeId(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "_";
                var result = new StringBuilder();
                foreach (char c in s)
                    result.Append(char.IsLetterOrDigit(c) ? c : '_');
                var str = result.ToString();
                if (char.IsDigit(str[0])) str = "_" + str;
                return str;
            }

            private static string StripQuotes(string s)
                => s.Trim('"', '\'');

            public static string SemanticTypeToCSharp(string semantic, string customTypeName = null) => semantic?.ToLower() switch
            {
                "number" => "double",
                "int" => "int",
                "long" => "long",
                "float" => "float",
                "decimal" => "decimal",
                "string" => "string",
                "bool" => "bool",
                "object" => "object",
                "guid" => "Guid",
                "datetime" => "DateTime",
                "datetimeoffset" => "DateTimeOffset",
                "dateonly" => "DateOnly",
                "timeonly" => "TimeOnly",
                "timespan" => "TimeSpan",
                "weburl" => "Uri",
                "bytes" => "byte[]",
                "type" => "Type",  // <-- ADD THIS LINE
                "custom" => customTypeName ?? "object",
                _ => customTypeName ?? semantic ?? "object"
            };
            public static List<string> TopologicalSort(SessionData.Session session)
            {
                var inDegree = session.Nodes.ToDictionary(n => n.UUID, _ => 0);
                var adjacency = session.Nodes.ToDictionary(n => n.UUID, _ => new List<string>());

                foreach (var c in session.Connections)
                {
                    if (!inDegree.ContainsKey(c.Node1UUID) || !inDegree.ContainsKey(c.Node2UUID))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[GEN] Orphaned connection skipped: {c.Node1UUID}.{c.Node1Port} → {c.Node2UUID}.{c.Node2Port}");
                        continue;
                    }
                    adjacency[c.Node1UUID].Add(c.Node2UUID);
                    inDegree[c.Node2UUID]++;
                }

                var queue = new Queue<string>(session.Nodes.Where(n => inDegree[n.UUID] == 0).Select(n => n.UUID));
                var sorted = new List<string>();

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    sorted.Add(cur);
                    foreach (var next in adjacency[cur])
                        if (--inDegree[next] == 0) queue.Enqueue(next);
                }

                if (sorted.Count != session.Nodes.Count)
                    throw new Exception("Cycle detected in node graph.");

                return sorted;
            }
        }
    }
}
