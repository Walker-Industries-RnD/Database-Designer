using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Database_Designer
{
    // Runs exported projects on the developer's machine: the generated API,
    // and single NodeWalker graphs against the local database. Both need the
    // .NET SDK (the `dotnet` command) to be installed.
    public static class DevRunner
    {
        public const int DefaultApiPort = 5180;

        public static string ProjectDir(string dataDir, string user, string project) =>
            project == null ? null : Path.Combine(dataDir, user, "Projects", project);

        // The newest GeneratedDB/vN folder written by Build Project.
        public static string LatestBuild(string projectDir)
        {
            var root = projectDir == null ? null : Path.Combine(projectDir, "GeneratedDB");
            if (root == null || !Directory.Exists(root)) return null;
            return Directory.GetDirectories(root)
                .Select(d => (dir: d, name: Path.GetFileName(d)))
                .Where(x => x.name.StartsWith("v") && int.TryParse(x.name.Substring(1), out _))
                .OrderByDescending(x => int.Parse(x.name.Substring(1)))
                .Select(x => x.dir)
                .FirstOrDefault();
        }

        public static async Task<bool> HasDotnetSdk()
        {
            try
            {
                var (code, output) = await Run("dotnet", "--list-sdks", null, null, null, TimeSpan.FromSeconds(20));
                return code == 0 && output.Trim().Length > 0;
            }
            catch { return false; }
        }

        public static Process StartApi(string buildDir, string connectionString, int port, Action<string> onOutput)
        {
            var csproj = Path.Combine(buildDir, "API", "API.csproj");
            if (!File.Exists(csproj)) throw new FileNotFoundException("This build has no API folder. Run Build Project again.");
            var psi = new ProcessStartInfo("dotnet", $"run --project \"{csproj}\" --urls http://localhost:{port}")
            {
                WorkingDirectory = Path.GetDirectoryName(csproj),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Environment["ConnectionStrings__Default"] = connectionString;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["DOTNET_NOLOGO"] = "1";
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (s, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }

        public static void Stop(Process p)
        {
            try { if (p != null && !p.HasExited) p.Kill(entireProcessTree: true); } catch { }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        public sealed class GraphRun
        {
            public bool Ok { get; set; }
            public string Result { get; set; }
            public string Error { get; set; }
            public string Log { get; set; }
        }

        // Compiles one graph together with the build's models and calls it
        // inside a transaction that is rolled back unless keepChanges is set.
        public static async Task<GraphRun> RunGraph(string buildDir, string scriptCode, string className, string method,
            string argumentsJson, string connectionString, bool keepChanges, Action<string> onOutput, CancellationToken ct = default)
        {
            var models = Path.Combine(buildDir, "API", "Models");
            if (!Directory.Exists(models)) throw new DirectoryNotFoundException("This build has no API models. Run Build Project again.");

            var runner = Path.Combine(buildDir, "Runner");
            Directory.CreateDirectory(runner);
            var pariah = Path.Combine(buildDir, "API", "lib", "PariahCybersecurity.dll");
            WriteIfChanged(Path.Combine(runner, "Runner.csproj"), RunnerCsproj.Replace("{PARIAH}", File.Exists(pariah)
                ? "\n    <Reference Include=\"PariahCybersecurity\"><HintPath>../API/lib/PariahCybersecurity.dll</HintPath></Reference>"
                : ""));
            WriteIfChanged(Path.Combine(runner, "Program.cs"), RunnerProgram);
            WriteIfChanged(Path.Combine(runner, "RunnerTx.cs"), RunnerTx);
            // A graph's own DB transaction becomes a savepoint inside the
            // runner's transaction, so a test run can still be rolled back.
            WriteIfChanged(Path.Combine(runner, "Script.cs"), System.Text.RegularExpressions.Regex.Replace(scriptCode,
                @"([A-Za-z_][\w\.]*)\.Database\.BeginTransactionAsync\(\)", "RunnerTx.BeginAsync($1)"));
            var argsFile = Path.Combine(runner, "run.json");
            File.WriteAllText(argsFile, System.Text.Json.JsonSerializer.Serialize(new
            {
                ClassName = className,
                Method = method,
                Arguments = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson,
                Connection = connectionString,
                Keep = keepChanges,
                // Direct Postgres (PG:) nodes open their own connection, which
                // the runner's transaction doesn't cover.
                OwnConnection = scriptCode.Contains("new NpgsqlConnection("),
            }));

            var (code, output) = await Run("dotnet", $"run --project \"{Path.Combine(runner, "Runner.csproj")}\" -- \"{argsFile}\"",
                runner, new Dictionary<string, string> { ["DOTNET_NOLOGO"] = "1" }, onOutput, TimeSpan.FromMinutes(5), ct);
            try { File.Delete(argsFile); } catch { }

            var run = new GraphRun { Log = output };
            int r = output.IndexOf("@@RESULT", StringComparison.Ordinal);
            int e = output.IndexOf("@@ERROR", StringComparison.Ordinal);
            if (r >= 0)
            {
                run.Ok = true;
                run.Result = Between(output, r + "@@RESULT".Length);
            }
            else if (e >= 0)
            {
                run.Error = Between(output, e + "@@ERROR".Length);
                var where = FailingNode(output, scriptCode);
                if (where != null) run.Error += "\n" + where;
            }
            else
                run.Error = code == 0 ? "The graph ran but printed no result." : CompileErrors(output);
            return run;
        }

        // Maps the first Script.cs frame of the stack trace to the node
        // comment ("// <Node Title>") the compiler writes above each node.
        private static string FailingNode(string output, string scriptCode)
        {
            int t = output.IndexOf("@@TRACE", StringComparison.Ordinal);
            if (t < 0) return null;
            var m = System.Text.RegularExpressions.Regex.Match(output.Substring(t), @"Script\.cs:line (\d+)");
            if (!m.Success) return null;
            var lines = scriptCode.Replace("\r\n", "\n").Split('\n');
            int line = int.Parse(m.Groups[1].Value);
            for (int i = Math.Min(line, lines.Length) - 1; i >= 0; i--)
            {
                var c = System.Text.RegularExpressions.Regex.Match(lines[i], @"^\s*// (?!WARNING)(.+)$");
                if (c.Success) return $"(in node \"{c.Groups[1].Value.Trim()}\", generated line {line})";
                if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"^\s*public static")) break;
            }
            return $"(generated line {line})";
        }

        private static string Between(string text, int start)
        {
            int end = text.IndexOf("@@END", start, StringComparison.Ordinal);
            return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
        }

        private static string CompileErrors(string output)
        {
            var errors = output.Split('\n').Select(l => l.Trim()).Where(l => l.Contains("error CS")).Distinct().Take(8).ToList();
            if (errors.Count == 0) return output.Length > 2000 ? output.Substring(output.Length - 2000) : output;
            return "The graph doesn't compile:\n" + string.Join("\n", errors.Select(l =>
            {
                int i = l.IndexOf("error CS", StringComparison.Ordinal);
                var msg = l.Substring(i);
                int bracket = msg.LastIndexOf(" [", StringComparison.Ordinal);
                var where = l.Contains("Script.cs") ? "graph: " : "";
                return where + (bracket > 0 ? msg.Substring(0, bracket) : msg);
            }));
        }

        private static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return;
            File.WriteAllText(path, content);
        }

        public static async Task<(int Code, string Output)> Run(string file, string args, string workDir,
            IDictionary<string, string> env, Action<string> onOutput, TimeSpan timeout, CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                WorkingDirectory = workDir ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (env != null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
            var sb = new StringBuilder();
            using var p = new Process { StartInfo = psi };
            void Line(string l) { if (l == null) return; lock (sb) sb.AppendLine(l); onOutput?.Invoke(l); }
            p.OutputDataReceived += (s, e) => Line(e.Data);
            p.ErrorDataReceived += (s, e) => Line(e.Data);
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                Stop(p);
                Line(ct.IsCancellationRequested ? "Stopped." : $"Timed out after {timeout.TotalSeconds:0}s.");
                return (-1, sb.ToString());
            }
            p.WaitForExit();
            lock (sb) return (p.ExitCode, sb.ToString());
        }

        private const string RunnerCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>$(NoWarn);CS8600;CS8602;CS8603;CS8604;CS8618;CS8625;CS1998;CS0162</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""../API/Models/*.cs"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""8.0.11"" />
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""8.0.11"" />
    <PackageReference Include=""Dapper"" Version=""2.1.35"" />{PARIAH}
  </ItemGroup>
</Project>";

        private const string RunnerTx = @"using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

// Turns a graph's own BeginTransaction into a savepoint of the runner's
// transaction: Commit releases it, Rollback (or leaving without commit)
// rolls back to it.
public static class RunnerTx
{
    private static int _count;

    public static Task<IDbContextTransaction> BeginAsync(DbContext db)
    {
        var outer = db.Database.CurrentTransaction;
        if (outer == null) return db.Database.BeginTransactionAsync();
        var name = ""dd_graph_"" + (++_count);
        outer.CreateSavepoint(name);
        return Task.FromResult<IDbContextTransaction>(new Nested(outer, name));
    }

    private sealed class Nested : IDbContextTransaction
    {
        private readonly IDbContextTransaction _outer;
        private readonly string _name;
        private bool _done;

        public Nested(IDbContextTransaction outer, string name) { _outer = outer; _name = name; }
        public Guid TransactionId => _outer.TransactionId;
        public void Commit() { if (_done) return; _done = true; _outer.ReleaseSavepoint(_name); }
        public Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return Task.CompletedTask; }
        public void Rollback() { if (_done) return; _done = true; _outer.RollbackToSavepoint(_name); }
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
        public void Dispose() { if (!_done) Rollback(); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
";

        private const string RunnerProgram = @"using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var run = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var className = run.GetProperty(""ClassName"").GetString()!;
var methodName = run.GetProperty(""Method"").GetString()!;
var arguments = JsonDocument.Parse(run.GetProperty(""Arguments"").GetString() ?? ""{}"").RootElement;
var keep = run.GetProperty(""Keep"").GetBoolean();
var ownConnection = run.GetProperty(""OwnConnection"").GetBoolean();

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(run.GetProperty(""Connection"").GetString()).Options;
using var db = new AppDbContext(options);
using var tx = db.Database.BeginTransaction();
var json = new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles, PropertyNameCaseInsensitive = true };

try
{
    var type = Assembly.GetExecutingAssembly().GetTypes().First(t => t.Name == className);
    var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == methodName);
    var inputs = method.GetParameters().Where(p => p.ParameterType != typeof(AppDbContext) && p.ParameterType != typeof(HttpClient)).ToList();
    var props = arguments.ValueKind == JsonValueKind.Object ? arguments.EnumerateObject().ToList() : new List<JsonProperty>();
    // Like the API: a single object input may be given as the whole JSON body.
    bool wholeBody = inputs.Count == 1 && inputs[0].ParameterType.IsClass && inputs[0].ParameterType != typeof(string)
        && !props.Any(x => string.Equals(x.Name, inputs[0].Name, StringComparison.OrdinalIgnoreCase));
    var values = method.GetParameters().Select(p =>
    {
        if (p.ParameterType == typeof(AppDbContext)) return db;
        if (p.ParameterType == typeof(HttpClient)) return new HttpClient();
        if (wholeBody) return arguments.Deserialize(p.ParameterType, json);
        foreach (var prop in props)
            if (string.Equals(prop.Name, p.Name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.Deserialize(p.ParameterType, json);
        return p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
    }).ToArray();

    object? result = method.Invoke(null, values);
    if (result is Task task)
    {
        await task;
        result = task.GetType().IsGenericType ? task.GetType().GetProperty(""Result"")!.GetValue(task) : null;
        if (result != null && result.GetType().FullName == ""System.Threading.Tasks.VoidTaskResult"") result = null;
    }
    if (keep) tx.Commit(); else tx.Rollback();
    Console.WriteLine(""@@RESULT"");
    Console.WriteLine(result == null ? ""(no output)"" : JsonSerializer.Serialize(result, result.GetType(), json));
    Console.WriteLine(keep ? ""(changes kept)"" : ""(database changes were rolled back)"");
    if (ownConnection) Console.WriteLine(""(PG: nodes use their own connection; their changes are not rolled back)"");
    Console.WriteLine(""@@END"");
}
catch (Exception ex)
{
    while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
    tx.Rollback();
    Console.WriteLine(""@@ERROR"");
    Console.WriteLine(ex.Data.Contains(""DD.FailIf"") ? ""Fail If stopped the graph: "" + ex.Message : ex.GetType().Name + "": "" + ex.Message);
    if (ex.InnerException != null) Console.WriteLine(""  "" + ex.InnerException.Message);
    Console.WriteLine(""@@END"");
    Console.WriteLine(""@@TRACE"");
    Console.WriteLine(ex.StackTrace);
}
";
    }
}
