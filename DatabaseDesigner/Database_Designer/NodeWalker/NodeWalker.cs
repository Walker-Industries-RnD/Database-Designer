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

// Sessions are saved as plain JSON (System.Text.Json). To protect one,
// encrypt the JSON string before writing it to disk.

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

        // Execution flow. Data wires say where a value comes from; exec wires
        // say what runs next. Every non-pure node has an implicit exec input
        // (ExecIn) and exec output (ExecOut). Flow nodes such as Branch add
        // their own exec outputs (SemanticType "exec"), and whatever is wired
        // to one of those only runs when that path is taken.
        public static class Flow
        {
            public const string ExecIn = "__exec";
            public const string ExecOut = "__then";
            public const string ExecSemantic = "exec";

            public static readonly HashSet<string> FlowTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "branch", "sequence", "for each", "repeat", "try", "return",
            };

            // Data outputs of a flow node that only exist inside one of its
            // paths (the loop variable only exists inside the loop body).
            public static string ScopeOfOutput(BareNode node, string port)
            {
                var t = node?.Title?.Trim().ToLowerInvariant();
                return t switch
                {
                    "for each" when port is "Item" or "Index" => "Loop Body",
                    "repeat" when port == "Index" => "Loop Body",
                    "try" when port == "Error" => "Catch",
                    _ => null
                };
            }

            public static bool IsFlowNode(BareNode node) =>
                node?.Title != null && FlowTitles.Contains(node.Title.Trim());

            public static bool IsExecPort(string name) => name == ExecIn || name == ExecOut;

            public static bool IsExecOutput(BareNode node, string port) =>
                port == ExecOut || (node?.Outputs?.Any(o => o.Name == port && IsExecType(o.SemanticType)) ?? false);

            public static bool IsExecType(string semantic) =>
                string.Equals(semantic, ExecSemantic, StringComparison.OrdinalIgnoreCase);

            public static bool IsExecConnection(Connection c, BareNode from) =>
                c.Node2Port == ExecIn || IsExecOutput(from, c.Node1Port);

            // Pure nodes only compute a value, so they don't get exec pins.
            private static readonly string[] _purePrefixes = { "where:", "select:", "logic:", "list:" };
            private static readonly HashSet<string> _pureTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "add", "subtract", "multiply", "divide", "and", "or", "not", "if",
                "equals", "not equals", "less than", "greater than", "less or equal", "greater or equal",
                "concat", "format", "weburl", "lambda", "cast", "expose", "env var",
                "event input", "custom input", "get variable", "null",
                "http: read json field",
            };

            public static bool IsPure(BareNode node)
            {
                var t = node?.Title?.Trim();
                if (string.IsNullOrEmpty(t)) return false;
                if (IsFlowNode(node)) return false;
                if (_pureTitles.Contains(t)) return true;
                if (t.EndsWith("literal", StringComparison.OrdinalIgnoreCase)) return true;
                return _purePrefixes.Any(p => t.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            }

            public static bool HasExecIn(BareNode node) => !IsPure(node);

            // On a flow node the implicit exec output means "after the whole
            // if/loop/try has finished".
            public static bool HasExecOut(BareNode node) =>
                !IsPure(node) && !string.Equals(node.Title?.Trim(), "return", StringComparison.OrdinalIgnoreCase);

            public static string ExecOutLabel(BareNode node) => IsFlowNode(node) ? "After" : "Then";
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
                // with the compiler's defaults and any "using ...;" lines
                // hoisted out of custom-node Logic before being emitted.
                public List<string> Usings { get; set; } = new();

                // Persisted viewport state. Following the pattern used by
                // Godot's GraphEdit, n8n, and ComfyUI: store the user's pan
                // offset (and reserve room for zoom) so reloading a session
                // restores the exact view. HasViewport disambiguates a saved
                // (0,0) viewport from a legacy file with no viewport state at
                // all - old files take the fit-to-content code path instead.
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
                        if (input.CustomTypeName is "AppDbContext" or "HttpClient") continue;
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
                    if (!n1.Outputs.Any(o => o.Name == c.Node1Port) && !n1.Inputs.Any(i => i.Name == c.Node1Port) && c.Node1Port != Flow.ExecOut)
                        warnings.Add(new ConnectionWarning { Connection = c, Reason = "Node1 port missing" });

                    if (!n2.Inputs.Any(i => i.Name == c.Node2Port) && !n2.Outputs.Any(o => o.Name == c.Node2Port) && c.Node2Port != Flow.ExecIn)
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
                // node position and reloads them as (0,0,0) - i.e. all nodes
                // collapse to the canvas origin on every load.
                IncludeFields = true
            };

            // Save / Load

            /// <summary>
            /// True if the session file exists, with or without the .json extension.
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

                return DeserializeSession(await File.ReadAllTextAsync(path));
            }

            // Gives every node without a saved position one, in columns that
            // follow the wires left to right (graphs built in code or shipped in
            // templates have no layout). Nodes that already have a position stay.
            public static int LayoutMissingNodes(SessionData.Session session)
            {
                var nodes = session.Nodes?.ToList() ?? new();
                session.NodePositions ??= new();
                var missing = nodes.Where(n => !session.NodePositions.ContainsKey(n.UUID)).ToList();
                if (missing.Count == 0) return 0;

                var ids = new HashSet<string>(nodes.Select(n => n.UUID));
                var wires = (session.Connections ?? new()).Where(c => ids.Contains(c.Node1UUID) && ids.Contains(c.Node2UUID)).ToList();
                var depth = nodes.ToDictionary(n => n.UUID, _ => 0);
                // Longest path from a source; bounded so a wiring loop can't spin forever.
                for (int pass = 0; pass < nodes.Count; pass++)
                {
                    bool changed = false;
                    foreach (var c in wires)
                        if (depth[c.Node2UUID] < depth[c.Node1UUID] + 1 && depth[c.Node1UUID] + 1 <= nodes.Count)
                        {
                            depth[c.Node2UUID] = depth[c.Node1UUID] + 1;
                            changed = true;
                        }
                    if (!changed) break;
                }

                float startX = 40, startY = 40;
                if (session.NodePositions.Count > 0)
                {
                    startX = session.NodePositions.Values.Max(v => v.X) + 340;
                    startY = session.NodePositions.Values.Min(v => v.Y);
                }
                const float columnWidth = 320, gap = 30;
                foreach (var column in missing.GroupBy(n => depth[n.UUID]).OrderBy(g => g.Key))
                {
                    float y = startY;
                    foreach (var n in column)
                    {
                        session.NodePositions[n.UUID] = new System.Numerics.Vector3(startX + column.Key * columnWidth, y, 0);
                        int rows = Math.Max((n.Inputs?.Count ?? 0) + 1, (n.Outputs?.Count ?? 0) + 1);
                        y += 60 + rows * 22 + gap;
                    }
                }
                return missing.Count;
            }

            public static string SerializeSession(SessionData.Session session) =>
                JsonSerializer.Serialize(session, _jsonOptions);

            public static SessionData.Session DeserializeSession(string json)
            {
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

            // The graph's content with positions made relative to the top-left
            // node, so re-centring the view doesn't count as an edit.
            public static string ContentKey(SessionData.Session session)
            {
                var pos = session.NodePositions ?? new();
                float minX = pos.Count > 0 ? pos.Values.Min(v => v.X) : 0;
                float minY = pos.Count > 0 ? pos.Values.Min(v => v.Y) : 0;
                return JsonSerializer.Serialize(new
                {
                    Nodes = session.Nodes.OrderBy(n => n.UUID, StringComparer.Ordinal),
                    Connections = session.Connections.Select(c => $"{c.Node1UUID}.{c.Node1Port}>{c.Node2UUID}.{c.Node2Port}").OrderBy(c => c, StringComparer.Ordinal),
                    Positions = pos.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value.X - minX:0}:{kv.Value.Y - minY:0}"),
                    Chunks = (session.Chunk ?? new()).Select(c => new { c.Name, c.Description, L = Math.Round(c.Left - minX), T = Math.Round(c.Top - minY), c.Width, c.Height, c.BorderColor }),
                    Notes = (session.Notes ?? new()).Select(n => new { n.Name, n.Description, L = Math.Round(n.Left - minX), T = Math.Round(n.Top - minY) }),
                    session.CustomScripts,
                    session.FunctionName,
                    session.IsAsync,
                    session.Usings,
                    session.Name,
                    session.Description,
                }, _jsonOptions);
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
                               || node1.Inputs.Any(i => i.Name == connection.Node1Port)
                               || (connection.Node1Port == Flow.ExecOut && Flow.HasExecOut(node1));
                bool validPort2 = node2.Inputs.Any(i => i.Name == connection.Node2Port)
                               || node2.Outputs.Any(o => o.Name == connection.Node2Port)
                               || (connection.Node2Port == Flow.ExecIn && Flow.HasExecIn(node2));

                if (!validPort1 || !validPort2)
                    throw new Exception($"Invalid port(s): {connection.Node1Port} / {connection.Node2Port}");

                bool execOut = Flow.IsExecOutput(node1, connection.Node1Port);
                bool execIn = connection.Node2Port == Flow.ExecIn;
                if (execOut != execIn)
                    throw new Exception("Exec wires connect an exec output to an exec input.");
                if (connection.Node1UUID == connection.Node2UUID)
                    throw new Exception("A node can't connect to itself.");

                if (execOut)
                {
                    // An exec output leads to exactly one next node (use a
                    // Sequence to run several), but many paths may merge into
                    // one exec input.
                    session.Connections.RemoveWhere(c =>
                        c.Node1UUID == connection.Node1UUID &&
                        c.Node1Port == connection.Node1Port);
                }
                else
                {
                    // Each data input accepts only ONE incoming connection:
                    // dragging a new wire into a port replaces the old one.
                    session.Connections.RemoveWhere(c =>
                        c.Node2UUID == connection.Node2UUID &&
                        c.Node2Port == connection.Node2Port);
                }

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
            /// remapping port <paramref name="findPort"/> -> <paramref name="replacePort"/> in all connections.
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

                    bool port1Ok = n1.Outputs.Any(o => o.Name == connection.Node1Port) || n1.Inputs.Any(i => i.Name == connection.Node1Port)
                                   || (connection.Node1Port == Flow.ExecOut && Flow.HasExecOut(n1));
                    bool port2Ok = n2.Inputs.Any(i => i.Name == connection.Node2Port) || n2.Outputs.Any(o => o.Name == connection.Node2Port)
                                   || (connection.Node2Port == Flow.ExecIn && Flow.HasExecIn(n2));
                    return !port1Ok || !port2Ok;
                }).ToList();
            }
        }

        // Compiler - generates actual drop-in C# code

        public static class Compiler
        {
            /// <summary>
            /// Compiles a session into a full C# script. The output is shaped as:
            ///
            ///     // header
            ///     using ...; using ...;        <- collected (defaults + session.Usings +
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
            // Describes the compiled entry point so external generators (e.g.
            // the API build) can call it: the wrapper class name, the method
            // name, whether it's async, whether it returns a value, and whether
            // it needs caller-supplied parameters.
            public static (string ClassName, string Method, bool IsAsync, bool ReturnsValue, bool HasParams)
                DescribeEntry(SessionData.Session session)
            {
                var className = SafeId(string.IsNullOrWhiteSpace(session.Name) ? "Generated" : session.Name);
                if (!className.EndsWith("Script", StringComparison.Ordinal)) className += "Script";

                var method   = SafeId(session.FunctionName ?? "DoACoolThing");
                bool isAsync = Regex.IsMatch(CompileToScript(session),
                    $@"public static async Task(<[^\r\n]*>)? {Regex.Escape(method)}\(");
                bool returns = GetReturnType(session) != "void";
                bool hasParams =
                    (session.Nodes?.Any(n => n.Title == "Event Input" || n.Title == "Custom Input") ?? false);

                return (className, method, isAsync, returns, hasParams);
            }

            public static List<(string Type, string Name)> DescribeParameters(SessionData.Session session)
            {
                var result = new List<(string, string)>();
                var method = SafeId(session.FunctionName ?? "DoACoolThing");
                var m = Regex.Match(CompileToScript(session), $@"public static [^\r\n]*? {Regex.Escape(method)}\((?<p>[^\r\n]*)\)");
                if (!m.Success) return result;
                var raw = m.Groups["p"].Value;
                int depth = 0, start = 0;
                for (int i = 0; i <= raw.Length; i++)
                {
                    if (i < raw.Length)
                    {
                        var ch = raw[i];
                        if (ch == '<' || ch == '(' || ch == '[') depth++;
                        else if (ch == '>' || ch == ')' || ch == ']') depth--;
                        if (!(ch == ',' && depth == 0)) continue;
                    }
                    var part = raw.Substring(start, i - start).Trim();
                    start = i + 1;
                    if (part.Length == 0) continue;
                    var sp = part.LastIndexOf(' ');
                    if (sp > 0) result.Add((part.Substring(0, sp).Trim(), part.Substring(sp + 1).Trim()));
                }
                return result;
            }

            public static string CompileToScript(SessionData.Session session)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"// {session.Name ?? "Unnamed"}");
                sb.AppendLine($"// {session.Nodes?.Count ?? 0} node(s), {session.Connections?.Count ?? 0} connection(s)");
                sb.AppendLine();

                // 1) Usings - defaults + session-level + those hoisted out of any node Logic
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

                EmitHelpers(sb, session);

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
                "type literal", "string literal", "int literal", "float literal", "bool literal", "null",
                "json literal", "connection string literal", "predicate literal", "custom input", "custom literal",
                "expose", "run after",
                "branch", "sequence", "for each", "repeat", "try", "return",
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
                "where: contains", "where: and", "where: or", "where: not", "select: field",
                "where: greater or equal", "where: less or equal", "where: starts with", "where: ends with",
                "where: like", "where: is null", "where: is not null", "where: is true", "where: is false",
                "where: between", "where: in",
                "db: query", "db: select field", "db: delete where",
                "list: filter", "list: first where", "list: any where", "list: count where",
                "list: sort by", "list: map field", "list: sum field",
                "list literal", "http: new request",
                "fail if", "env var", "set field", "object: build",
                "db: update where", "db: increment where", "db: decrement where",
                "http: post form", "http: read json field",
                "db: order by", "db: order by desc", "db: page", "db: include",
                "db: rls enable", "db: rls disable", "db: rls force",
                "db: rls create policy", "db: rls drop policy",
                "db: rls set user", "db: rls reset user", "db: rls set role", "db: raw sql",
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
                "where: contains", "where: and", "where: or", "where: not", "select: field",
                "where: greater or equal", "where: less or equal", "where: starts with", "where: ends with",
                "where: like", "where: is null", "where: is not null", "where: is true", "where: is false",
                "where: between", "where: in",
                "db: query", "db: select field", "db: delete where",
                "list: filter", "list: first where", "list: any where", "list: count where",
                "list: sort by", "list: map field", "list: sum field",
                "list literal", "http: new request",
                "fail if", "env var", "set field", "object: build",
                "db: update where", "db: increment where", "db: decrement where",
                "http: post form", "http: read json field",
                "db: order by", "db: order by desc", "db: page", "db: include",
                "db: rls enable", "db: rls disable", "db: rls force",
                "db: rls create policy", "db: rls drop policy",
                "db: rls set user", "db: rls reset user", "db: rls set role", "db: raw sql",
                "where: greater or equal", "where: less or equal", "where: starts with", "where: ends with",
                "where: like", "where: is null", "where: is not null", "where: is true", "where: is false",
                "where: between", "where: in", "db: query", "db: select field", "db: delete where",
                "db: update where", "db: increment where", "db: decrement where",
            };

            // SpacetimeDB titles -> auto-add the `SpacetimeDB.Types` namespace.
            private static readonly HashSet<string> _sdbTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "sdb: connect", "sdb: disconnect", "sdb: subscribe", "sdb: call reducer",
                "sdb: iter table", "sdb: find by pk",
                "sdb: on insert", "sdb: on update", "sdb: on delete",
            };

            // HTTP titles -> auto-add System.Net.Http and System.Net.Http.Json.
            private static readonly HashSet<string> _httpTitles = new(StringComparer.OrdinalIgnoreCase)
            {
                "http: new client", "http: get", "http: post json", "http: put json",
                "http: delete", "http: send",
                "http: read json", "http: read string", "http: status code",
                "http: set bearer token", "http: set header", "http: ensure success",
                "http: new request", "http: post form", "http: read json field",
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
                    set.Add("System.Text");
                    set.Add("System.Text.Json");
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

            private static void EmitHelpers(StringBuilder sb, SessionData.Session session)
            {
                var titles = new HashSet<string>((session.Nodes ?? new()).Select(n => n.Title?.Trim().ToLowerInvariant() ?? ""));
                if (titles.Overlaps(new[] { "equals", "not equals", "less than", "greater than", "less or equal", "greater or equal" }))
                {
                    sb.AppendLine();
                    sb.AppendLine("    // Comparison helpers for the Equals / Greater / Less blocks. Numbers are");
                    sb.AppendLine("    // compared by value whatever their type (int, long, double, decimal...),");
                    sb.AppendLine("    // numeric text is read as a number, and anything else falls back to text.");
                    sb.AppendLine("    private static bool __IsNumber(object v) => v is byte || v is sbyte || v is short || v is ushort || v is int || v is uint || v is long || v is ulong || v is float || v is double || v is decimal;");
                    sb.AppendLine("    private static bool __AsNumber(object v, out decimal d, out double f)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        d = 0; f = 0;");
                    sb.AppendLine("        if (__IsNumber(v)) { f = Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); try { d = Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture); } catch (OverflowException) { d = 0; } return true; }");
                    sb.AppendLine("        if (v is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) { decimal.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d); return true; }");
                    sb.AppendLine("        return false;");
                    sb.AppendLine("    }");
                    sb.AppendLine("    private static int __Compare(object a, object b)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (a == null || b == null) return a == null ? (b == null ? 0 : -1) : 1;");
                    sb.AppendLine("        if ((__IsNumber(a) || __IsNumber(b)) && __AsNumber(a, out var da, out var fa) && __AsNumber(b, out var db, out var fb))");
                    sb.AppendLine("            return (a is decimal || b is decimal) ? da.CompareTo(db) : fa.CompareTo(fb);");
                    sb.AppendLine("        if (a is DateTimeOffset ao && b is DateTime bt) return ao.CompareTo(new DateTimeOffset(bt));");
                    sb.AppendLine("        if (a is DateTime at && b is DateTimeOffset bo) return new DateTimeOffset(at).CompareTo(bo);");
                    sb.AppendLine("        if (a.GetType() == b.GetType() && a is IComparable same) return same.CompareTo(b);");
                    sb.AppendLine("        if (a is IComparable ca) { try { return ca.CompareTo(Convert.ChangeType(b, a.GetType(), System.Globalization.CultureInfo.InvariantCulture)); } catch { } }");
                    sb.AppendLine("        return string.Compare(Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture), Convert.ToString(b, System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);");
                    sb.AppendLine("    }");
                    sb.AppendLine("    private static bool __Same(object a, object b)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (a == null || b == null) return a == null && b == null;");
                    sb.AppendLine("        if (Equals(a, b)) return true;");
                    sb.AppendLine("        if (__IsNumber(a) || __IsNumber(b) || a.GetType() != b.GetType()) return __Compare(a, b) == 0;");
                    sb.AppendLine("        return false;");
                    sb.AppendLine("    }");
                }
                if (titles.Contains("set field"))
                {
                    sb.AppendLine();
                    sb.AppendLine("    // Sets obj.<name> = value, converting value to the property's type.");
                    sb.AppendLine("    private static void __SetField(object target, string name, object value)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (target == null) throw new InvalidOperationException($\"Set Field: object is null (setting {name}).\");");
                    sb.AppendLine("        var p = target.GetType().GetProperty(name) ?? throw new InvalidOperationException($\"{target.GetType().Name} has no property {name}.\");");
                    sb.AppendLine("        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;");
                    sb.AppendLine("        object v = value == null ? null");
                    sb.AppendLine("            : t.IsInstanceOfType(value) ? value");
                    sb.AppendLine("            : t == typeof(Guid) ? Guid.Parse(value.ToString())");
                    sb.AppendLine("            : t.IsEnum ? Enum.Parse(t, value.ToString())");
                    sb.AppendLine("            : t == typeof(DateTimeOffset) ? DateTimeOffset.Parse(value.ToString(), System.Globalization.CultureInfo.InvariantCulture)");
                    sb.AppendLine("            : Convert.ChangeType(value, t, System.Globalization.CultureInfo.InvariantCulture);");
                    sb.AppendLine("        p.SetValue(target, v);");
                    sb.AppendLine("    }");
                }
                if (titles.Contains("http: read json field"))
                {
                    sb.AppendLine();
                    sb.AppendLine("    // Reads a dotted path (\"a.b.0.c\") from a JSON document as a string.");
                    sb.AppendLine("    private static string __JsonField(string json, string path)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (string.IsNullOrWhiteSpace(json)) return string.Empty;");
                    sb.AppendLine("        using var doc = System.Text.Json.JsonDocument.Parse(json);");
                    sb.AppendLine("        var el = doc.RootElement;");
                    sb.AppendLine("        foreach (var part in (path ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries))");
                    sb.AppendLine("        {");
                    sb.AppendLine("            if (el.ValueKind == System.Text.Json.JsonValueKind.Array && int.TryParse(part, out var i) && i < el.GetArrayLength()) el = el[i];");
                    sb.AppendLine("            else if (el.ValueKind == System.Text.Json.JsonValueKind.Object && el.TryGetProperty(part, out var next)) el = next;");
                    sb.AppendLine("            else return string.Empty;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : el.ToString();");
                    sb.AppendLine("    }");
                }
            }

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

            private static void EmitMainMethod(StringBuilder outSb, SessionData.Session session)
            {
                var sb = new StringBuilder();
                bool isAsync = session.IsAsync || (session.Nodes?.Any(n => n.IsAsync) ?? false);
                string returnType = GetReturnType(session);

                var inputNodes  = session.Nodes?.Where(n => n.Title == "Event Input").ToList() ?? new();
                var outputNodes = session.Nodes?.Where(n => n.Title == "Set Output").ToList() ?? new();
                // Custom Input nodes also become method parameters - their CLR
                // type comes from the CUSTOMINPUT(<Type>) marker stored in Logic.
                var customInputNodes = session.Nodes?.Where(n => n.Title == "Custom Input").ToList() ?? new();

                var paramList = inputNodes
                    .SelectMany(n => n.Outputs)
                    .Select(o => $"{SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName)} {SafeId(o.Name)}")
                    .ToList();
                _customInputNames = new Dictionary<string, string>();
                var usedParams = new HashSet<string>(paramList.Select(p => p.Split(' ').Last()), StringComparer.Ordinal);
                foreach (var n in customInputNodes)
                {
                    var port = n.Outputs.FirstOrDefault();
                    if (port == null) continue;
                    var (markerType, markerName) = ParseCustomInput(n.Logic);
                    var typeName = markerType
                        ?? (!string.IsNullOrEmpty(port.CustomTypeName)
                            ? port.CustomTypeName
                            : SemanticTypeToCSharp(port.SemanticType, port.CustomTypeName));
                    var baseName = SafeId(markerName ?? port.Name);
                    var paramName = baseName;
                    for (int k = 2; !usedParams.Add(paramName); k++) paramName = baseName + k;
                    _customInputNames[n.UUID] = paramName;
                    paramList.Add($"{typeName} {paramName}");
                }

                var funcName = SafeId(session.FunctionName ?? "DoACoolThing");
                var parameters = string.Join(", ", paramList);

                void WriteSignature()
                {
                    string methodModifier;
                    if (isAsync)
                        methodModifier = returnType == "void"
                            ? "public static async Task"
                            : $"public static async Task<{returnType}>";
                    else
                        methodModifier = $"public static {returnType}";
                    outSb.AppendLine($"    {methodModifier} {funcName}({parameters})");
                    outSb.AppendLine("    {");
                }

                if (session.Nodes == null || session.Nodes.Count == 0)
                {
                    WriteSignature();
                    outSb.AppendLine("        // No nodes; add nodes to generate code");
                    outSb.AppendLine("    }");
                    return;
                }

                if (session.Nodes.Any(n => n.Title is "Get Variable" or "Set Variable"))
                {
                    sb.AppendLine("        var variables = new Dictionary<string, object>();");
                    sb.AppendLine();
                }

                var portWarnings = SessionData.ValidateRequiredPorts(session);
                foreach (var w in portWarnings)
                    sb.AppendLine($"        // WARNING: {w}");
                if (portWarnings.Any()) sb.AppendLine();

                var nodes = session.Nodes.ToDictionary(n => n.UUID);
                var execConns = session.Connections
                    .Where(c => nodes.ContainsKey(c.Node1UUID) && nodes.ContainsKey(c.Node2UUID)
                                && Flow.IsExecConnection(c, nodes[c.Node1UUID]))
                    .ToHashSet();
                var reverseMap = session.Connections
                    .Where(c => !execConns.Contains(c))
                    .GroupBy(c => c.Node2UUID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Predicate map gets reset per compile so re-entry doesn't
                // leak lambda text from the previous run.
                _predicateExpressions = new Dictionary<string, string>();
                _predicateIR          = new Dictionary<string, PredExpr>();
                _declaredTypes        = new Dictionary<string, string>();

                List<string> order;
                try { order = TopologicalSort(session); }
                catch { order = session.Nodes.Select(n => n.UUID).ToList(); }

                var declaredVars = new HashSet<string>();
                var plan = new FlowPlan(nodes, session.Connections.Where(c => nodes.ContainsKey(c.Node1UUID) && nodes.ContainsKey(c.Node2UUID)).ToList(), execConns, order);

                string ReturnExpr()
                {
                    if (returnType == "void") return null;
                    var outVars = outputNodes.SelectMany(n => n.Inputs).Select(i => OutputLocal(i.Name)).Distinct().ToList();
                    return outVars.Count == 1 ? outVars[0] : outVars.Count > 1 ? $"({string.Join(", ", outVars)})" : null;
                }

                if (plan.UsesFlow)
                {
                    // A Set Output may sit inside a branch, so the results are
                    // declared up front and the branches only assign them.
                    foreach (var inp in outputNodes.SelectMany(n => n.Inputs))
                        if (declaredVars.Add(OutputLocal(inp.Name)))
                            sb.AppendLine($"        {SemanticTypeToCSharp(inp.SemanticType, inp.CustomTypeName)} {OutputLocal(inp.Name)} = default;");
                    foreach (var w in plan.Warnings)
                        sb.AppendLine($"        // WARNING: {w}");
                    sb.AppendLine();
                }

                void EmitBlock(string key, string indent)
                {
                    foreach (var uuid in plan.Members(key))
                        EmitNode(nodes[uuid], indent);
                }

                void EmitScoped(string key, string indent, Action<HashSet<string>> declare = null)
                {
                    var saved = new HashSet<string>(declaredVars);
                    declare?.Invoke(declaredVars);
                    EmitBlock(key, indent);
                    declaredVars.Clear();
                    declaredVars.UnionWith(saved);
                }

                void EmitNode(BareNode node, string indent)
                {
                    var incoming = reverseMap.TryGetValue(node.UUID, out var inc) ? inc : new();
                    var inputValues = incoming.GroupBy(c => c.Node2Port).ToDictionary(g => g.Key, g =>
                    {
                        var c = g.First();
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

                    if (Flow.IsFlowNode(node))
                    {
                        EmitFlowNode(node, inputValues, incoming, indent);
                        return;
                    }

                    // Per-port context the codegen cases can read.
                    var inputTypes    = new Dictionary<string, string>();
                    var inputLiterals = new Dictionary<string, string>();
                    var inputUpstream = new Dictionary<string, string>();
                    var incomingIR    = new Dictionary<string, PredExpr>();
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
                        if (_predicateIR != null &&
                            _predicateIR.TryGetValue($"{c.Node1UUID}:{c.Node1Port}", out var upIr))
                            incomingIR[c.Node2Port] = upIr;
                    }
                    _inputTypeContext    = inputTypes;
                    _inputLiteralContext = inputLiterals;
                    _inputUpstreamTitle  = inputUpstream;
                    _incomingPredicateIR = incomingIR;
                    _currentNodeUuid     = node.UUID;

                    string code = GenerateNodeCode(node, inputValues, declaredVars, isAsync);
                    _inputTypeContext    = null;
                    _inputLiteralContext = null;
                    _inputUpstreamTitle  = null;
                    _incomingPredicateIR = null;
                    _currentNodeUuid     = null;
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        sb.AppendLine($"{indent}// {node.Title}");
                        foreach (var line in code.Split('\n'))
                            if (!string.IsNullOrWhiteSpace(line))
                                sb.AppendLine(indent + line.TrimEnd());
                        sb.AppendLine();
                    }
                }

                void EmitFlowNode(BareNode node, Dictionary<string, string> iv, List<Connection> incoming, string indent)
                {
                    var inner = indent + "    ";
                    string Child(string port) => FlowPlan.Key(node.UUID, port);
                    bool Has(string port) => plan.Members(Child(port)).Count > 0;
                    void Open() => sb.AppendLine($"{indent}{{");
                    void Close()
                    {
                        var nl = Environment.NewLine;
                        if (sb.Length >= 2 * nl.Length && sb.ToString(sb.Length - 2 * nl.Length, 2 * nl.Length) == nl + nl)
                            sb.Length -= nl.Length;
                        sb.AppendLine($"{indent}}}");
                    }

                    sb.AppendLine($"{indent}// {node.Title}");
                    switch (node.Title.Trim().ToLowerInvariant())
                    {
                        case "branch":
                        {
                            var cond = Inp(iv, "Condition", "false");
                            var src = incoming.FirstOrDefault(c => c.Node2Port == "Condition");
                            bool isBool = cond is "true" or "false"
                                || (src != null && nodes.TryGetValue(src.Node1UUID, out var sn)
                                    && string.Equals(sn.Outputs?.FirstOrDefault(o => o.Name == src.Node1Port)?.SemanticType, "bool", StringComparison.OrdinalIgnoreCase));
                            var test = isBool ? cond : TruthyExpr(cond);
                            bool hasTrue = Has("True"), hasFalse = Has("False");
                            if (!hasTrue && !hasFalse)
                            {
                                sb.AppendLine($"{indent}// nothing is connected to True or False");
                                break;
                            }
                            sb.AppendLine(hasTrue ? $"{indent}if ({test})" : $"{indent}if (!({test}))");
                            Open(); EmitScoped(Child(hasTrue ? "True" : "False"), inner); Close();
                            if (hasTrue && hasFalse)
                            {
                                sb.AppendLine($"{indent}else");
                                Open(); EmitScoped(Child("False"), inner); Close();
                            }
                            break;
                        }
                        case "sequence":
                        {
                            // Each step runs in the same scope, so a later step
                            // can use values made by an earlier one.
                            foreach (var port in node.Outputs.Where(o => Flow.IsExecType(o.SemanticType)).Select(o => o.Name)
                                         .OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal))
                                EmitBlock(Child(port), indent);
                            break;
                        }
                        case "for each":
                        {
                            var (items, elem) = ListInput(iv);
                            var itemVar = NodeVarName(node, "Item");
                            var indexVar = NodeVarName(node, "Index");
                            _declaredTypes[itemVar] = elem;
                            _declaredTypes[indexVar] = "int";
                            sb.AppendLine($"{indent}var {indexVar} = -1;");
                            declaredVars.Add(indexVar);
                            sb.AppendLine($"{indent}foreach (var {itemVar} in {items})");
                            Open();
                            sb.AppendLine($"{inner}{indexVar}++;");
                            EmitScoped(Child("Loop Body"), inner, d => d.Add(itemVar));
                            Close();
                            break;
                        }
                        case "repeat":
                        {
                            var count = Inp(iv, "Count", "0");
                            var indexVar = NodeVarName(node, "Index");
                            _declaredTypes[indexVar] = "int";
                            sb.AppendLine($"{indent}for (int {indexVar} = 0; {indexVar} < Convert.ToInt32({count}); {indexVar}++)");
                            Open(); EmitScoped(Child("Loop Body"), inner, d => d.Add(indexVar)); Close();
                            break;
                        }
                        case "try":
                        {
                            var errVar = NodeVarName(node, "Error");
                            var exVar = "__ex_" + NodeShortId(node);
                            _declaredTypes[errVar] = "string";
                            sb.AppendLine($"{indent}try");
                            Open(); EmitScoped(Child("Try"), inner); Close();
                            sb.AppendLine($"{indent}catch (Exception {exVar})");
                            Open();
                            sb.AppendLine($"{inner}string {errVar} = {exVar}.Message;");
                            EmitScoped(Child("Catch"), inner, d => d.Add(errVar));
                            Close();
                            break;
                        }
                        case "return":
                        {
                            var r = ReturnExpr();
                            sb.AppendLine(r == null ? $"{indent}return;" : $"{indent}return {r};");
                            break;
                        }
                    }
                    sb.AppendLine();
                }

                if (plan.UsesFlow)
                    EmitBlock("", "        ");
                else
                    foreach (var uuid in order)
                        if (nodes.TryGetValue(uuid, out var node)) EmitNode(node, "        ");

                var ret = ReturnExpr();
                if (ret != null) sb.AppendLine($"        return {ret};");

                var bodyText = sb.ToString();
                if (_implicitDbRx.IsMatch(bodyText) && !paramList.Any(p => p.EndsWith(" _db")))
                    paramList.Add("AppDbContext _db");
                if (_implicitHttpRx.IsMatch(bodyText) && !paramList.Any(p => p.EndsWith(" _http")))
                    paramList.Add("HttpClient _http");
                parameters = string.Join(", ", paramList);

                if (!isAsync && _awaitRx.IsMatch(bodyText)) isAsync = true;
                WriteSignature();
                outSb.Append(sb);
                outSb.AppendLine("    }");
            }

            // Works out where each node's code goes when exec wires are used.
            // A "block" is the body of one exec output of a flow node (the
            // True side of a Branch, a loop body...), keyed "<uuid>|<port>";
            // "" is the method body itself.
            //  - A node with exec inputs goes in the block its exec wires come
            //    from (several paths merging -> the closest shared block, i.e.
            //    after the if/else).
            //  - Any other node goes in the deepest block one of its inputs
            //    comes from. Pure nodes (math, compares, Get Variable...) then
            //    sink toward the nodes that use them, so a value used inside a
            //    loop is re-read on every pass and a value only one side of a
            //    Branch needs is only computed on that side.
            private sealed class FlowPlan
            {
                public bool UsesFlow { get; }
                public List<string> Warnings { get; } = new();

                private readonly Dictionary<string, BareNode> _nodes;
                private readonly List<Connection> _all;
                private readonly HashSet<Connection> _exec;
                private readonly Dictionary<string, int> _rank;
                private readonly Dictionary<string, string> _blockOf = new();
                private readonly HashSet<string> _visiting = new();
                private readonly Dictionary<string, List<string>> _members = new();

                public FlowPlan(Dictionary<string, BareNode> nodes, List<Connection> all, HashSet<Connection> exec, List<string> order)
                {
                    _nodes = nodes;
                    _all = all;
                    _exec = exec;
                    _rank = new Dictionary<string, int>();
                    for (int i = 0; i < order.Count; i++) _rank[order[i]] = i;
                    foreach (var u in nodes.Keys) if (!_rank.ContainsKey(u)) _rank[u] = _rank.Count;
                    UsesFlow = exec.Count > 0 || nodes.Values.Any(Flow.IsFlowNode);
                    if (!UsesFlow) return;

                    foreach (var u in nodes.Keys.OrderBy(u => _rank[u])) Resolve(u);
                    Sink(order);
                    foreach (var g in nodes.Keys.GroupBy(u => _blockOf[u]))
                        _members[g.Key] = Order(g.Key, g.ToList());
                    CheckScopes();
                }

                public static string Key(string uuid, string port) => uuid + "|" + port;
                private static string OwnerOf(string key) => key.Substring(0, key.IndexOf('|'));
                private static string PortOf(string key) => key.Substring(key.IndexOf('|') + 1);

                public List<string> Members(string key) =>
                    _members.TryGetValue(key, out var m) ? m : new List<string>();

                private string Title(string uuid) => _nodes.TryGetValue(uuid, out var n) ? n.Title : "?";

                private string ParentOf(string key) =>
                    key == "" ? null : _nodes.ContainsKey(OwnerOf(key)) ? Resolve(OwnerOf(key)) : "";

                // The block and every block it sits inside, innermost first,
                // always ending with "".
                private List<string> Chain(string key)
                {
                    var list = new List<string>();
                    for (var k = key; k != null && list.Count < 64; k = ParentOf(k))
                    {
                        list.Add(k);
                        if (k == "") return list;
                    }
                    list.Add("");
                    return list;
                }

                private bool Contains(string outer, string inner) => Chain(inner).Contains(outer);

                private string Lca(string a, string b)
                {
                    var cb = Chain(b);
                    return Chain(a).FirstOrDefault(cb.Contains) ?? "";
                }

                // Sequence steps share their parent's scope.
                private bool IsInline(string key) =>
                    key != "" && _nodes.TryGetValue(OwnerOf(key), out var n) &&
                    string.Equals(n.Title?.Trim(), "sequence", StringComparison.OrdinalIgnoreCase);

                private string Visible(string key)
                {
                    for (int i = 0; key != "" && IsInline(key) && i < 64; i++) key = ParentOf(key);
                    return key;
                }

                private string PortBlock(string uuid, string port)
                {
                    var scope = Flow.ScopeOfOutput(_nodes[uuid], port);
                    return scope != null ? Key(uuid, scope) : Resolve(uuid);
                }

                private string Resolve(string uuid)
                {
                    if (_blockOf.TryGetValue(uuid, out var known)) return known;
                    if (!_visiting.Add(uuid)) return "";

                    string block = "";
                    var execIn = _exec.Where(c => c.Node2UUID == uuid).ToList();
                    if (execIn.Count > 0)
                    {
                        block = execIn
                            .Select(c => c.Node1Port == Flow.ExecOut ? Resolve(c.Node1UUID) : Key(c.Node1UUID, c.Node1Port))
                            .Distinct()
                            .Aggregate(Lca);
                    }
                    else
                    {
                        foreach (var d in _all.Where(c => c.Node2UUID == uuid && !_exec.Contains(c))
                                              .Select(c => PortBlock(c.Node1UUID, c.Node1Port)).Distinct().ToList())
                        {
                            if (Contains(block, d)) block = d;
                            else if (Contains(d, block)) continue;
                            else if (Visible(d) == Visible(block)) block = Visible(block);
                            else
                            {
                                Warnings.Add($"\"{Title(uuid)}\" uses values from two different paths; it can only run where both exist.");
                                if (Chain(d).Count > Chain(block).Count) block = d;
                            }
                        }
                    }

                    _visiting.Remove(uuid);
                    _blockOf[uuid] = block;
                    return block;
                }

                private void Sink(List<string> order)
                {
                    var final = new Dictionary<string, string>();
                    string Final(string u) => final.TryGetValue(u, out var f) ? f : _blockOf[u];
                    foreach (var u in Enumerable.Reverse(order))
                    {
                        if (!_nodes.TryGetValue(u, out var node) || !Flow.IsPure(node)) continue;
                        if (_exec.Any(c => c.Node1UUID == u || c.Node2UUID == u)) continue;
                        var users = _all.Where(c => c.Node1UUID == u && !_exec.Contains(c) && _nodes.ContainsKey(c.Node2UUID))
                                        .Select(c => Final(c.Node2UUID)).Distinct().ToList();
                        if (users.Count == 0) continue;
                        var target = users.Aggregate(Lca);
                        if (Contains(_blockOf[u], target)) final[u] = target;
                    }
                    foreach (var kv in final) _blockOf[kv.Key] = kv.Value;
                }

                // The member of block `key` that contains node `uuid` (itself,
                // or the flow node whose path it is on), or null.
                private string Lift(string uuid, string key)
                {
                    var rep = uuid;
                    var blk = _blockOf.TryGetValue(uuid, out var b) ? b : "";
                    for (int i = 0; i < 64; i++)
                    {
                        if (blk == key) return rep;
                        if (blk == "") return null;
                        rep = OwnerOf(blk);
                        blk = _blockOf.TryGetValue(rep, out var nb) ? nb : "";
                    }
                    return null;
                }

                private List<string> Order(string key, List<string> members)
                {
                    var set = members.ToHashSet();
                    var adj = members.ToDictionary(m => m, _ => new HashSet<string>());
                    var indeg = members.ToDictionary(m => m, _ => 0);
                    foreach (var c in _all)
                    {
                        var a = Lift(c.Node1UUID, key);
                        var b = Lift(c.Node2UUID, key);
                        if (a == null || b == null || a == b || !set.Contains(a) || !set.Contains(b)) continue;
                        if (adj[a].Add(b)) indeg[b]++;
                    }
                    var ready = new SortedSet<(int, string)>(members.Where(m => indeg[m] == 0).Select(m => (_rank[m], m)));
                    var result = new List<string>();
                    while (ready.Count > 0)
                    {
                        var cur = ready.Min;
                        ready.Remove(cur);
                        result.Add(cur.Item2);
                        foreach (var n in adj[cur.Item2])
                            if (--indeg[n] == 0) ready.Add((_rank[n], n));
                    }
                    if (result.Count < members.Count)
                    {
                        Warnings.Add("The exec wires form a loop; some steps may run out of order.");
                        result.AddRange(members.Except(result).OrderBy(m => _rank[m]));
                        return result;
                    }

                    // Pure nodes are evaluated right before the first node that
                    // uses them, so a Get Variable after a loop sees the value
                    // the loop left behind.
                    var preds = members.ToDictionary(m => m, _ => new List<string>());
                    foreach (var kv in adj)
                        foreach (var b in kv.Value) preds[b].Add(kv.Key);
                    bool Pure(string u) => Flow.IsPure(_nodes[u]);
                    var placed = new HashSet<string>();
                    var lazy = new List<string>();
                    void Place(string u)
                    {
                        if (!placed.Add(u)) return;
                        foreach (var p in preds[u].Where(Pure).OrderBy(p => _rank[p])) Place(p);
                        lazy.Add(u);
                    }
                    foreach (var u in result.Where(u => !Pure(u))) Place(u);
                    foreach (var u in result.Where(Pure)) Place(u);
                    return lazy;
                }

                private void CheckScopes()
                {
                    foreach (var c in _all)
                    {
                        if (_exec.Contains(c)) continue;
                        var from = Visible(PortBlock(c.Node1UUID, c.Node1Port));
                        var to = _blockOf[c.Node2UUID];
                        if (!Contains(from, to) && !Contains(from, Visible(to)))
                            Warnings.Add($"\"{Title(c.Node2UUID)}\" reads {c.Node1Port} from \"{Title(c.Node1UUID)}\", which only exists inside the {PortOf(from)} path of \"{Title(OwnerOf(from))}\".");
                    }
                }
            }

            private static readonly Regex _implicitDbRx   = new(@"(?<![\w.""])_db\b", RegexOptions.Compiled);
            private static readonly Regex _implicitHttpRx = new(@"(?<![\w.""])_http\b", RegexOptions.Compiled);

            private static readonly Regex _awaitRx = new(@"(^|[^\w""])await\s", RegexOptions.Multiline | RegexOptions.Compiled);

            private static string GetReturnType(SessionData.Session session)
            {
                var outputNodes = session.Nodes?.Where(n => n.Title == "Set Output").ToList() ?? new();
                if (!outputNodes.Any()) return "void";
                // Several Set Output nodes may set the same output (one per
                // Branch path), so outputs are identified by name.
                var outputs = outputNodes.SelectMany(n => n.Inputs).GroupBy(i => i.Name).Select(g => g.First()).ToList();
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
                    case "start":
                        if (node.Outputs.Any(o => o.Name == "Flow"))
                            Declare(sb, declared, NodeVarName(node, "Flow"), "object", "null");
                        break;
                    case "end":
                        break;
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
                        foreach (var o in node.Outputs)
                            Declare(sb, declared, NodeVarName(node, o.Name),
                                SemanticTypeToCSharp(o.SemanticType, o.CustomTypeName), SafeId(o.Name));
                        break;
                    }
                    case "set output":
                    {
                        foreach (var inp in node.Inputs)
                        {
                            var val = inputValues.TryGetValue(inp.Name, out var v) ? v : "default";
                            var outVar = OutputLocal(inp.Name);
                            Declare(sb, declared, outVar, SemanticTypeToCSharp(inp.SemanticType, inp.CustomTypeName), val);
                        }
                        break;
                    }
                    case "add":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Convert.ToDouble({a}) + Convert.ToDouble({b})");
                        break;
                    }
                    case "subtract":
                    {
                        var a = Inp(inputValues, "A", "0");
                        var b = Inp(inputValues, "B", "0");
                        DeclareExpr(sb, declared, node, "Result", $"Convert.ToDouble({a}) - Convert.ToDouble({b})");
                        break;
                    }
                    case "multiply":
                    {
                        var a = Inp(inputValues, "A", "1");
                        var b = Inp(inputValues, "B", "1");
                        DeclareExpr(sb, declared, node, "Result", $"Convert.ToDouble({a}) * Convert.ToDouble({b})");
                        break;
                    }
                    case "divide":
                    {
                        var a = Inp(inputValues, "A", "1");
                        var b = Inp(inputValues, "B", "1");
                        sb.AppendLine($"if (Convert.ToDouble({b}) == 0) throw new DivideByZeroException(\"Divide node '{node.Title}': divisor is zero.\");");
                        DeclareExpr(sb, declared, node, "Result", $"Convert.ToDouble({a}) / Convert.ToDouble({b})");
                        break;
                    }
                    case "if":
                    {
                        var cond = Inp(inputValues, "Condition", "null");
                        var trueVar = NodeVarName(node, "True");
                        Declare(sb, declared, trueVar, "bool", TruthyExpr(cond));
                        Declare(sb, declared, NodeVarName(node, "False"), "bool", $"!{trueVar}");
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
                        var objType = _declaredTypes != null && _declaredTypes.TryGetValue(obj, out var ot) ? ot : null;
                        var propId = SanitiseTypeName(prop.Trim());
                        if (objType != null && objType is not ("object" or "var" or "dynamic") && !objType.StartsWith("List<") && !objType.EndsWith("[]"))
                            DeclareTyped(sb, declared, node, "Value", "var", $"{obj}.{propId}");
                        else
                            DeclareExpr(sb, declared, node, "Value",
                                $"({obj})?.GetType().GetProperty(\"{EscapeString(prop.Trim())}\")?.GetValue({obj})");
                        break;
                    }
                    case "cast":
                    {
                        var obj = Inp(inputValues, "Value", Inp(inputValues, "Object", "null"));
                        var targetType = TypeFromConnectedPort("Type") ?? ExtractLiteralValue(node.Logic) ?? "object";
                        targetType = targetType.Trim('"', '\'');
                        DeclareExpr(sb, declared, node, "Result",
                            targetType == "object" ? $"({obj})" : $"({targetType}){obj}");
                        break;
                    }
                    case "run after":
                    {
                        var after = Inp(inputValues, "After", "null");
                        var value = inputValues.TryGetValue("Value", out var v) && !string.IsNullOrWhiteSpace(v) ? v : after;
                        DeclareTyped(sb, declared, node, "Then", KnownType(value), value);
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

                    // Comparisons accept any two values. The helpers compare numbers
                    // by value, so an int against a double or decimal works.
                    case "equals":
                    case "not equals":
                    {
                        var a = Inp(inputValues, "A", "null");
                        var b = Inp(inputValues, "B", "null");
                        var op = title == "equals" ? "" : "!";
                        DeclareExpr(sb, declared, node, "Result",
                            $"{op}__Same({a}, {b})");
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
                            $"__Compare({a}, {b}) {op}");
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
                        var table = SqlHole(Inp(inputValues, "Table", "\"table\""));
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
                        sb.AppendLine($"using (var __nc = new NpgsqlCommand($\"NOTIFY {SqlHole(ch)}, '{SqlHole(pay)}'\", {conn})) await __nc.ExecuteNonQueryAsync();");
                        break;
                    }
                    case "pg: listen":
                    {
                        var conn = Inp(inputValues, "Connection", "null");
                        var ch   = Inp(inputValues, "Channel", "\"\"");
                        var cb   = Inp(inputValues, "Callback", "(_, __) => {}");
                        sb.AppendLine($"using (var __lc = new NpgsqlCommand($\"LISTEN {SqlHole(ch)}\", {conn})) await __lc.ExecuteNonQueryAsync();");
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
                        var a0 = Inp(inputValues, "Arg0", "null");
                        var a1 = Inp(inputValues, "Arg1", "null");
                        var a2 = Inp(inputValues, "Arg2", "null");
                        DeclareExpr(sb, declared, node, "Result",
                            $"string.Format(System.Globalization.CultureInfo.InvariantCulture, {tpl}, {a0}, {a1}, {a2})");
                        break;
                    }
                    case "weburl":
                    {
                        var url = Inp(inputValues, "URL", "\"https://example.com\"");
                        DeclareExpr(sb, declared, node, "URI", $"new Uri({url})", customType: "Uri");
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

                    // Literals - runtime value comes from the inline editor,
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
                        // Auto-detect: JSON-shaped value -> JsonNode.Parse, else
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
                        if (_predicateIR != null && !string.IsNullOrEmpty(_currentNodeUuid))
                            _predicateIR[$"{_currentNodeUuid}:Value"] = new PredRaw { Text = lit };
                        break;
                    }
                    case "custom literal":
                    {
                        // Logic is "CUSTOMLIT::<TypeName>\n<body>". The body is
                        // auto-detected: JSON-shaped -> Deserialize<T>(@"...");
                        // anything else -> treat as a plain string literal of type T.
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
                            // Fall back to deserialising a JSON-encoded string -
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
                        // CUSTOMINPUT(<Type>) - the value travels in as a parameter to
                        // the generated function. Emit a passthrough so wires resolve.
                        var (typeName, _) = ParseCustomInput(node.Logic);
                        var portName = node.Outputs.FirstOrDefault()?.Name ?? "Value";
                        var paramName = _customInputNames != null && _customInputNames.TryGetValue(node.UUID, out var pn) ? pn : SafeId(portName);
                        DeclareTyped(sb, declared, node, portName, typeName ?? "object", paramName);
                        break;
                    }

                    // EF Core: Easy - beginner-friendly nodes that read like English.
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
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().ToListAsync()");
                        break;
                    }
                    case "db: get one by id":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var idGOBI  = Inp(inputValues, "Id", "0");
                        DeclareTyped(sb, declared, node, "Row", ent,
                            $"await {db}.Set<{ent}>().FindAsync({idGOBI})");
                        break;
                    }
                    case "db: get where":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().Where({pred}).ToListAsync()");
                        break;
                    }
                    case "db: get first":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        DeclareTyped(sb, declared, node, "Row", ent,
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
                        if (node.Outputs.Any(o => o.Name == "Saved"))
                            DeclareTyped(sb, declared, node, "Saved", KnownType(en), en);
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

                    // Predicate builders
                    // Each emits NO local variable - they produce a lambda
                    // expression text that's stored in _predicateExpressions
                    // and inlined at the consumer's call site.
                    case "select: field":
                    {
                        var prop = SanitiseTypeName(LiteralFromConnectedPort("Property")
                                                    ?? StripQuotesExpr(Inp(inputValues, "Property", "Property")));
                        StorePredicate("Selector", $"x => x.{prop}");
                        break;
                    }
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
                        var cmp = new PredCompare { Property = prop, Value = val };
                        switch (title)
                        {
                            case "where: equals":     cmp.Op = "=="; break;
                            case "where: not equals": cmp.Op = "!="; break;
                            case "where: greater":    cmp.Op = ">";  break;
                            case "where: less":       cmp.Op = "<";  break;
                            case "where: contains":   cmp.Contains = true; break;
                        }
                        StorePredicateIR("Predicate", cmp);
                        break;
                    }
                    case "fail if":
                    {
                        var cond = Inp(inputValues, "Condition", "false");
                        var msg = Inp(inputValues, "Message", "\"Request failed.\"");
                        sb.AppendLine($"if ({TruthyExpr(cond)}) throw new InvalidOperationException(Convert.ToString({msg})) {{ Data = {{ [\"DD.FailIf\"] = true }} }};");
                        if (node.Outputs.Any(o => o.Name == "Ok"))
                            DeclareTyped(sb, declared, node, "Ok", "bool", "true");
                        break;
                    }
                    case "object: build":
                    {
                        var pairs = new List<string>();
                        for (int k = 1; k <= 6; k++)
                            if (inputValues.TryGetValue($"Key{k}", out var key) && !string.IsNullOrWhiteSpace(key))
                                pairs.Add($"[Convert.ToString({key})] = {Inp(inputValues, $"Value{k}", "null")}");
                        DeclareTyped(sb, declared, node, "Object", "Dictionary<string, object>",
                            $"new Dictionary<string, object> {{ {string.Join(", ", pairs)} }}");
                        break;
                    }
                    case "env var":
                    {
                        var name = Inp(inputValues, "Name", "\"\"");
                        DeclareTyped(sb, declared, node, "Value", "string", $"Environment.GetEnvironmentVariable({name}) ?? string.Empty");
                        break;
                    }
                    case "set field":
                    {
                        var obj = Inp(inputValues, "Object", "null");
                        var prop = PropertyInput(inputValues);
                        var val = Inp(inputValues, "Value", "null");
                        sb.AppendLine($"__SetField({obj}, \"{EscapeString(prop)}\", {val});");
                        break;
                    }
                    case "db: update where":
                    case "db: increment where":
                    case "db: decrement where":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => false");
                        var prop = PropertyInput(inputValues);
                        string setter = title switch
                        {
                            "db: increment where" => $"x => x.{prop} + {Inp(inputValues, "Amount", "1")}",
                            "db: decrement where" => $"x => x.{prop} - {Inp(inputValues, "Amount", "1")}",
                            _ => Inp(inputValues, "Value", "default")
                        };
                        DeclareTyped(sb, declared, node, "Affected", "int",
                            $"await {db}.Set<{ent}>().Where({pred}).ExecuteUpdateAsync(s => s.SetProperty(x => x.{prop}, {setter}))");
                        break;
                    }
                    case "http: post form":
                    {
                        var cli  = Inp(inputValues, "Client", "_http");
                        var url  = Inp(inputValues, "Url", "\"\"");
                        var form = Inp(inputValues, "Form", "\"\"");
                        DeclareTyped(sb, declared, node, "Response", "HttpResponseMessage",
                            $"await {cli}.PostAsync({url}, new StringContent(Convert.ToString({form}) ?? \"\", Encoding.UTF8, \"application/x-www-form-urlencoded\"))");
                        break;
                    }
                    case "http: read json field":
                    {
                        var resp = Inp(inputValues, "Response", "null");
                        var path = Inp(inputValues, "Path", "\"\"");
                        DeclareTyped(sb, declared, node, "Value", "string",
                            $"__JsonField(await {resp}.Content.ReadAsStringAsync(), Convert.ToString({path}))");
                        break;
                    }
                    case "where: greater or equal":
                    case "where: less or equal":
                    {
                        var prop = PropertyInput(inputValues);
                        var val  = Inp(inputValues, "Value", "default");
                        StorePredicateIR("Predicate", new PredCompare
                        {
                            Property = prop, Value = val,
                            Op = title == "where: greater or equal" ? ">=" : "<="
                        });
                        break;
                    }
                    case "where: starts with":
                    case "where: ends with":
                    {
                        var prop = PropertyInput(inputValues);
                        var val  = Inp(inputValues, "Value", "\"\"");
                        var m    = title == "where: starts with" ? "StartsWith" : "EndsWith";
                        StorePredicateIR("Predicate", new PredText(p => $"{p}.{prop}.{m}({val})"));
                        break;
                    }
                    case "where: like":
                    {
                        var prop = PropertyInput(inputValues);
                        var pat  = Inp(inputValues, "Pattern", "\"%\"");
                        StorePredicateIR("Predicate", new PredText(p => $"EF.Functions.ILike({p}.{prop}, {pat})"));
                        break;
                    }
                    case "where: is null":
                    case "where: is not null":
                    {
                        var prop = PropertyInput(inputValues);
                        var op   = title == "where: is null" ? "==" : "!=";
                        StorePredicateIR("Predicate", new PredText(p => $"{p}.{prop} {op} null"));
                        break;
                    }
                    case "where: is true":
                    case "where: is false":
                    {
                        var prop = PropertyInput(inputValues);
                        var val  = title == "where: is true" ? "true" : "false";
                        StorePredicateIR("Predicate", new PredText(p => $"{p}.{prop} == {val}"));
                        break;
                    }
                    case "where: between":
                    {
                        var prop = PropertyInput(inputValues);
                        var min  = Inp(inputValues, "Min", "default");
                        var max  = Inp(inputValues, "Max", "default");
                        StorePredicateIR("Predicate", new PredText(p => $"{p}.{prop} >= {min} && {p}.{prop} <= {max}"));
                        break;
                    }
                    case "where: in":
                    {
                        var prop = PropertyInput(inputValues);
                        var vals = Inp(inputValues, "Values", "Array.Empty<object>()");
                        StorePredicateIR("Predicate", new PredText(p => $"Enumerable.Contains({vals}, {p}.{prop})"));
                        break;
                    }

                    case "db: query":
                    {
                        var db  = Inp(inputValues, "Db", "_db");
                        var ent = ResolveEntityType(inputValues);
                        var qv  = $"__q_{NodeShortId(node)}";
                        sb.AppendLine($"IQueryable<{ent}> {qv} = {db}.Set<{ent}>();");
                        if (inputValues.TryGetValue("Predicate", out var pred) && !string.IsNullOrWhiteSpace(pred))
                            sb.AppendLine($"{qv} = {qv}.Where({pred});");
                        if (inputValues.TryGetValue("OrderBy", out var key) && !string.IsNullOrWhiteSpace(key))
                        {
                            if (inputValues.TryGetValue("Descending", out var desc) && !string.IsNullOrWhiteSpace(desc))
                                sb.AppendLine($"{qv} = {desc} ? {qv}.OrderByDescending({key}) : {qv}.OrderBy({key});");
                            else
                                sb.AppendLine($"{qv} = {qv}.OrderBy({key});");
                        }
                        if (inputValues.TryGetValue("Skip", out var skip) && !string.IsNullOrWhiteSpace(skip))
                            sb.AppendLine($"{qv} = {qv}.Skip({skip});");
                        if (inputValues.TryGetValue("Take", out var take) && !string.IsNullOrWhiteSpace(take))
                            sb.AppendLine($"{qv} = {qv}.Take({take});");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>", $"await {qv}.ToListAsync()");
                        break;
                    }
                    case "db: select field":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var sel  = Inp(inputValues, "Selector", "x => x");
                        var where = inputValues.TryGetValue("Predicate", out var pr) && !string.IsNullOrWhiteSpace(pr) ? $".Where({pr})" : "";
                        DeclareTyped(sb, declared, node, "Values", "var",
                            $"await {db}.Set<{ent}>(){where}.Select({sel}).ToListAsync()");
                        break;
                    }
                    case "db: delete where":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => false");
                        DeclareTyped(sb, declared, node, "Affected", "int",
                            $"await {db}.Set<{ent}>().Where({pred}).ExecuteDeleteAsync()");
                        break;
                    }

                    case "list: filter":
                    case "list: first where":
                    case "list: any where":
                    case "list: count where":
                    {
                        var (items, elem) = ListInput(inputValues);
                        var pred = Inp(inputValues, "Predicate", "x => true");
                        switch (title)
                        {
                            case "list: filter":
                                DeclareTyped(sb, declared, node, "Items", $"List<{elem}>", $"{items}.Where({pred}).ToList()"); break;
                            case "list: first where":
                                DeclareTyped(sb, declared, node, "Item", elem, $"{items}.FirstOrDefault({pred})"); break;
                            case "list: any where":
                                DeclareTyped(sb, declared, node, "Result", "bool", $"{items}.Any({pred})"); break;
                            default:
                                DeclareTyped(sb, declared, node, "Count", "int", $"{items}.Count({pred})"); break;
                        }
                        break;
                    }
                    case "list: sort by":
                    {
                        var (items, elem) = ListInput(inputValues);
                        var key  = Inp(inputValues, "Selector", "x => x");
                        var desc = Inp(inputValues, "Descending", "false");
                        DeclareTyped(sb, declared, node, "Items", $"List<{elem}>",
                            $"({desc} ? {items}.OrderByDescending({key}) : {items}.OrderBy({key})).ToList()");
                        break;
                    }
                    case "list: map field":
                    {
                        var (items, _) = ListInput(inputValues);
                        var sel = Inp(inputValues, "Selector", "x => x");
                        DeclareTyped(sb, declared, node, "Values", "var", $"{items}.Select({sel}).ToList()");
                        break;
                    }
                    case "list: sum field":
                    {
                        var (items, elem) = ListInput(inputValues);
                        var sel = Inp(inputValues, "Selector", "x => 0");
                        var body = elem == "dynamic" ? $"(decimal)({LambdaBody(sel)})" : $"Convert.ToDecimal({LambdaBody(sel)})";
                        DeclareTyped(sb, declared, node, "Total", "decimal", $"{items}.Sum(x => {body})");
                        break;
                    }

                    case "list literal":
                    {
                        var raw = ExtractLiteralValue(node.Logic) ?? "";
                        var parts = raw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                        bool allInts = parts.Count > 0 && parts.All(p => long.TryParse(p, out _));
                        var arr = allInts
                            ? $"new long[] {{ {string.Join(", ", parts)} }}"
                            : $"new string[] {{ {string.Join(", ", parts.Select(p => "\"" + StripQuotes(p).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""))} }}";
                        DeclareTyped(sb, declared, node, "Values", allInts ? "long[]" : "string[]", arr);
                        break;
                    }
                    case "http: new request":
                    {
                        var method = StripQuotesExpr(Inp(inputValues, "Method", "\"GET\""));
                        var url    = Inp(inputValues, "Url", "\"\"");
                        var methodExpr = inputValues.TryGetValue("Method", out var mv) && !(mv ?? "").Trim().StartsWith("\"")
                            ? $"new HttpMethod({mv})"
                            : $"new HttpMethod(\"{method.ToUpperInvariant()}\")";
                        var content = inputValues.TryGetValue("Body", out var body) && !string.IsNullOrWhiteSpace(body)
                            ? $", Content = JsonContent.Create((object)({body}))" : "";
                        DeclareTyped(sb, declared, node, "Request", "HttpRequestMessage",
                            $"new HttpRequestMessage({methodExpr}, {url}) {{ Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionOrLower{content} }}");
                        break;
                    }
                    case "where: and":
                    case "where: or":
                    {
                        var a  = IncomingPred(inputValues, "A");
                        var b  = IncomingPred(inputValues, "B");
                        var op = title == "where: and" ? "&&" : "||";
                        StorePredicateIR("Predicate", new PredBool { Op = op, Left = a, Right = b });
                        break;
                    }
                    case "where: not":
                    {
                        var inner = IncomingPred(inputValues, "Predicate");
                        StorePredicateIR("Predicate", new PredNot { Inner = inner });
                        break;
                    }

                    // Result-shaping helpers
                    case "db: order by":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var key  = Inp(inputValues, "KeySelector", "x => x");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().OrderBy({key}).ToListAsync()");
                        break;
                    }
                    case "db: order by desc":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var key  = Inp(inputValues, "KeySelector", "x => x");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().OrderByDescending({key}).ToListAsync()");
                        break;
                    }
                    case "db: page":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var skip = Inp(inputValues, "Skip", "0");
                        var take = Inp(inputValues, "Take", "50");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().Skip({skip}).Take({take}).ToListAsync()");
                        break;
                    }
                    case "db: include":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var ent  = ResolveEntityType(inputValues);
                        var nav  = Inp(inputValues, "Navigation", "x => x");
                        DeclareTyped(sb, declared, node, "Rows", $"List<{ent}>",
                            $"await {db}.Set<{ent}>().Include({nav}).ToListAsync()");
                        break;
                    }

                    // Row-Level Security (Postgres). All emit raw SQL via
                    // Database.ExecuteSqlRawAsync - table/policy names are
                    // identifier-quoted; values use parameter binding where
                    // it works, else string.Format with NpgsqlDbCommand.
                    case "db: rls enable":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = SqlHole(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{t}\\\" ENABLE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls disable":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = SqlHole(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{t}\\\" DISABLE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls force":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var t  = SqlHole(Inp(inputValues, "Table", "\"table\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"ALTER TABLE \\\"{t}\\\" FORCE ROW LEVEL SECURITY\")");
                        break;
                    }
                    case "db: rls create policy":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var t    = SqlHole(Inp(inputValues, "Table", "\"table\""));
                        var name = SqlHole(Inp(inputValues, "PolicyName", "\"policy\""));
                        var op   = SqlHole(Inp(inputValues, "Operation", "\"ALL\""));
                        var role = SqlHole(Inp(inputValues, "Role", "\"PUBLIC\""));
                        var usng = SqlHole(Inp(inputValues, "Using", "\"true\""));
                        // WithCheck is optional - emit the WITH CHECK clause only if present.
                        var checkVal = inputValues.TryGetValue("WithCheck", out var wc) ? wc : null;
                        var checkClause = string.IsNullOrEmpty(checkVal)
                            ? ""
                            : $" WITH CHECK ({SqlHole(checkVal)})";
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"CREATE POLICY \\\"{name}\\\" ON \\\"{t}\\\" FOR {op} TO {role} USING ({usng}){checkClause}\")");
                        break;
                    }
                    case "db: rls drop policy":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var t    = SqlHole(Inp(inputValues, "Table", "\"table\""));
                        var name = SqlHole(Inp(inputValues, "PolicyName", "\"policy\""));
                        DeclareExpr(sb, declared, node, "Affected",
                            $"await {db}.Database.ExecuteSqlRawAsync($\"DROP POLICY IF EXISTS \\\"{name}\\\" ON \\\"{t}\\\"\")");
                        break;
                    }
                    case "db: rls set user":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        var u  = Inp(inputValues, "UserId", "\"\"");
                        sb.AppendLine($"await {db}.Database.OpenConnectionAsync();");
                        sb.AppendLine($"await {db}.Database.ExecuteSqlRawAsync(\"SELECT set_config('app.current_user_id', {{0}}, false)\", Convert.ToString({u}) ?? \"\");");
                        break;
                    }
                    case "db: rls reset user":
                    {
                        var db = Inp(inputValues, "Db", "_db");
                        sb.AppendLine($"await {db}.Database.ExecuteSqlRawAsync(\"RESET app.current_user_id; RESET app.current_user_role; RESET ROLE\");");
                        break;
                    }
                    case "db: rls set role":
                    {
                        var db   = Inp(inputValues, "Db", "_db");
                        var role = Inp(inputValues, "Role", "\"standard_users\"");
                        sb.AppendLine($"await {db}.Database.OpenConnectionAsync();");
                        sb.AppendLine($"await {db}.Database.ExecuteSqlRawAsync(\"SELECT set_config('app.current_user_role', {{0}}, false), set_config('role', {{0}}, false)\", {role});");
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
                            $"Content = JsonContent.Create((object)({body})) }})",
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
                        // doesn't apply here - we read it from the output port's
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

                    // Postgres / Npgsql - Dapper-style call sites. The compiler
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
                            $"$\"INSERT INTO {SqlHole(table)} ({{string.Join(\",\", {entity}.GetType().GetProperties().Select(p => p.Name))}}) \" + " +
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
                            $"$\"UPDATE {SqlHole(table)} SET {{string.Join(\",\", {entity}.GetType().GetProperties().Where(p => p.Name != \"Id\").Select(p => p.Name + \"=@\" + p.Name))}} WHERE id = @__id\", " +
                            $"((Func<Dapper.DynamicParameters>)(() => {{ var __p = new Dapper.DynamicParameters({entity}); __p.Add(\"__id\", {idUPG}); return __p; }}))())");
                        break;
                    }
                    case "pg: delete by id":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var table = Inp(inputValues, "Table", "\"table\"");
                        var idDPG    = Inp(inputValues, "Id", "0");
                        DeclareExpr(sb, declared, node, "Affected",
                            $"Dapper.SqlMapper.Execute({conn}, $\"DELETE FROM {SqlHole(table)} WHERE id = @id\", new {{ id = {idDPG} }})");
                        break;
                    }
                    case "pg: count":
                    {
                        var conn  = Inp(inputValues, "Connection", "null");
                        var table = Inp(inputValues, "Table", "\"table\"");
                        var where = Inp(inputValues, "Where", "\"1=1\"");
                        var pars  = Inp(inputValues, "Params", "null");
                        DeclareExpr(sb, declared, node, "Count",
                            $"Dapper.SqlMapper.ExecuteScalar<long>({conn}, $\"SELECT COUNT(*) FROM {SqlHole(table)} WHERE {SqlHole(where)}\", {pars})");
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
                        // Two-output node - use a tuple deconstruction.
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

                    // SSO - same _auth instance, but using the system-level
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
                        // Emit `new TypeName { Prop1 = inA, Prop2 = inB, ... }`
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
                        // this bucket - "// MyMethod - implement here" /
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
                            // User wrote real code in Logic - emit verbatim.
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

            private static void DeclareTyped(StringBuilder sb, HashSet<string> declared, BareNode node, string portName, string typeName, string expr)
            {
                var varName = NodeVarName(node, portName);
                if (_declaredTypes != null) _declaredTypes[varName] = typeName;
                Declare(sb, declared, varName, typeName, expr);
            }

            [ThreadStatic] private static Dictionary<string, string> _declaredTypes;
            [ThreadStatic] private static Dictionary<string, string> _customInputNames;

            public static (string Type, string Name) ParseCustomInput(string logic)
            {
                var m = Regex.Match(logic ?? "", @"^\s*CUSTOMINPUT\s*\(\s*([\w\.<>\[\],?]+)(?:\s+([A-Za-z_]\w*))?\s*\)\s*$");
                return m.Success ? (m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null) : (null, null);
            }

            private static string KnownType(string expr) =>
                _declaredTypes != null && _declaredTypes.TryGetValue(expr ?? "", out var t) && !string.IsNullOrEmpty(t) ? t : "var";

            private static string PropertyInput(Dictionary<string, string> inputValues) =>
                SanitiseTypeName(LiteralFromConnectedPort("Property")
                                 ?? StripQuotesExpr(Inp(inputValues, "Property", "Property")));

            private static (string items, string elem) ListInput(Dictionary<string, string> inputValues)
            {
                var items = Inp(inputValues, "Items", "Array.Empty<object>()");
                if (_declaredTypes != null && _declaredTypes.TryGetValue(items, out var t) && t != null)
                {
                    if (t.StartsWith("List<", StringComparison.Ordinal) && t.EndsWith(">", StringComparison.Ordinal))
                        return (items, t.Substring(5, t.Length - 6));
                    if (t.EndsWith("[]", StringComparison.Ordinal))
                        return (items, t.Substring(0, t.Length - 2));
                }
                return ($"System.Linq.Enumerable.Cast<dynamic>((System.Collections.IEnumerable)({items}))", "dynamic");
            }

            private sealed class PredText : PredExpr
            {
                private readonly Func<string, string> _render;
                public PredText(Func<string, string> render) => _render = render;
                public override string RenderBody(string p) => _render(p);
            }

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
            [ThreadStatic] private static Dictionary<string, PredExpr> _predicateIR;
            [ThreadStatic] private static Dictionary<string, PredExpr> _incomingPredicateIR;
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
            // Pull `x => ...body...` apart, returning just the body so multiple
            // predicates can be combined under a single lambda parameter.
            private static string LambdaBody(string lambda)
            {
                if (string.IsNullOrEmpty(lambda)) return "true";
                var i = lambda.IndexOf("=>", StringComparison.Ordinal);
                return i < 0 ? lambda : lambda.Substring(i + 2).Trim();
            }

            private abstract class PredExpr
            {
                public abstract string RenderBody(string param);
                public string RenderLambda(string param = "x") => $"{param} => {RenderBody(param)}";
            }

            private sealed class PredCompare : PredExpr
            {
                public string Property;
                public string Op;
                public string Value;
                public bool   Contains;
                public override string RenderBody(string p) =>
                    Contains ? $"{p}.{Property}.Contains({Value})"
                             : $"{p}.{Property} {Op} {Value}";
            }

            private sealed class PredBool : PredExpr
            {
                public string   Op;
                public PredExpr Left, Right;
                public override string RenderBody(string p) =>
                    $"({Left.RenderBody(p)}) {Op} ({Right.RenderBody(p)})";
            }

            private sealed class PredNot : PredExpr
            {
                public PredExpr Inner;
                public override string RenderBody(string p) => $"!({Inner.RenderBody(p)})";
            }

            private sealed class PredRaw : PredExpr
            {
                public string Text;
                public override string RenderBody(string p)
                {
                    if (string.IsNullOrEmpty(Text)) return "true";
                    var i = Text.IndexOf("=>", StringComparison.Ordinal);
                    if (i < 0) return Text.Trim();
                    var origParam = Text.Substring(0, i).Trim().Trim('(', ')', ' ');
                    var body      = Text.Substring(i + 2).Trim();
                    if (!string.IsNullOrEmpty(origParam) && origParam != p &&
                        System.Text.RegularExpressions.Regex.IsMatch(origParam, "^[A-Za-z_][A-Za-z0-9_]*$"))
                        body = System.Text.RegularExpressions.Regex.Replace(
                            body,
                            $@"(?<![A-Za-z0-9_.]){System.Text.RegularExpressions.Regex.Escape(origParam)}\b",
                            p);
                    return body;
                }
            }

            private static void StorePredicateIR(string port, PredExpr ir)
            {
                if (ir == null) return;
                if (_predicateIR != null && !string.IsNullOrEmpty(_currentNodeUuid))
                    _predicateIR[$"{_currentNodeUuid}:{port}"] = ir;
                StorePredicate(port, ir.RenderLambda("x"));
            }

            private static PredExpr IncomingPred(Dictionary<string, string> inputValues, string port, string fallback = "x => true")
            {
                if (_incomingPredicateIR != null && _incomingPredicateIR.TryGetValue(port, out var ir) && ir != null)
                    return ir;
                return new PredRaw { Text = Inp(inputValues, port, fallback) };
            }

            private static string TypeFromConnectedPort(string portName) =>
                _inputTypeContext != null && _inputTypeContext.TryGetValue(portName, out var t)
                    ? t : null;

            // Resolve the entity-type name for a DB/PG node. Priority:
            //   1. Wired upstream port carries a CustomTypeName ("Game") -> use it.
            //   2. Upstream is a String/Custom Literal -> sanitise the raw text
            //      (skip comments, accept "class Foo" -> "Foo").
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

            // String -> "escaped" form safe to drop inside a "..." literal.
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
                $"((object)({raw})) switch {{ null => false, bool __b => __b, string __s => !string.IsNullOrEmpty(__s), " +
                $"int __i => __i != 0, long __l => __l != 0, double __d => __d != 0, decimal __m => __m != 0, _ => true }}";

            // For interpolated SQL: if the input arrived as a quoted string
            // ("table"), unwrap it so we don't render `"table"` inside the
            // generated $"..." literal. If it's already a variable reference,
            // leave it.
            private static string SqlHole(string expr)
            {
                if (string.IsNullOrEmpty(expr)) return "";
                var t = expr.Trim();
                if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
                    return t.Substring(1, t.Length - 2).Replace("{", "{{").Replace("}", "}}");
                return "{" + expr + "}";
            }

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

            private static string OutputLocal(string portName) => "result_" + SafeId(portName);

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
                "type" => "Type",
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
