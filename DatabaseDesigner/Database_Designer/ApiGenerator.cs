using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NodeCompiler   = Database_Designer.NodeWalker.NodeWalker.Compiler;
using NodeOperations = Database_Designer.NodeWalker.NodeWalker.Operations;

namespace Database_Designer
{
    public static class ApiGenerator
    {
        public sealed record ModelInfo(string ClassName, string QualifiedTableName);

        public sealed record ApiFunction(string Module, string Endpoint, string Function, string Verb, string Description);

        public static void Generate(
            string apiPath,
            IReadOnlyList<ModelInfo> models,
            string modelClassesSource,
            IReadOnlyList<ApiFunction> functions,
            string scriptsDir,
            string pariahDllPath = null)
        {
            Directory.CreateDirectory(apiPath);
            var modelsDir = Path.Combine(apiPath, "Models");
            var controllersDir = Path.Combine(apiPath, "Controllers");
            var logicDir = Path.Combine(controllersDir, "Logic");
            Directory.CreateDirectory(modelsDir);
            Directory.CreateDirectory(controllersDir);

            File.WriteAllText(Path.Combine(apiPath, "Program.cs"), ProgramCs);
            File.WriteAllText(Path.Combine(apiPath, "appsettings.json"), AppSettingsJson);

            File.WriteAllText(Path.Combine(modelsDir, "Models.cs"),
                "// Model classes generated from your Database Designer tables\n" +
                "using Microsoft.EntityFrameworkCore;\n" +
                (modelClassesSource ?? ""));
            File.WriteAllText(Path.Combine(modelsDir, "AppDbContext.cs"), BuildDbContext(models));
            File.WriteAllText(Path.Combine(modelsDir, "EntityAliases.cs"), BuildEntityAliases(models));

            bool usesPariah = false;
            foreach (var module in functions.GroupBy(f => f.Module ?? ""))
            {
                if (string.IsNullOrWhiteSpace(module.Key)) continue;
                var code = BuildController(module.Key, module.ToList(), scriptsDir, logicDir, ref usesPariah);
                File.WriteAllText(Path.Combine(controllersDir, Pascal(module.Key) + "Controller.cs"), code);
            }

            string pariahRef = "";
            if (usesPariah && !string.IsNullOrEmpty(pariahDllPath) && File.Exists(pariahDllPath))
            {
                var libDir = Path.Combine(apiPath, "lib");
                Directory.CreateDirectory(libDir);
                File.Copy(pariahDllPath, Path.Combine(libDir, "PariahCybersecurity.dll"), overwrite: true);
                pariahRef = "\n    <Reference Include=\"PariahCybersecurity\"><HintPath>lib/PariahCybersecurity.dll</HintPath></Reference>";
            }

            File.WriteAllText(Path.Combine(apiPath, "API.csproj"), CsProj.Replace("{PARIAH}", pariahRef));
        }


        private static string BuildDbContext(IReadOnlyList<ModelInfo> models)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine();
            sb.AppendLine("public class AppDbContext : DbContext");
            sb.AppendLine("{");
            sb.AppendLine("    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
            sb.AppendLine();
            sb.AppendLine("    public DbSet<SecureMedia> SecureMedia => Set<SecureMedia>();");
            sb.AppendLine("    public DbSet<SecureMediaSession> SecureMediaSessions => Set<SecureMediaSession>();");
            var used = new HashSet<string>(StringComparer.Ordinal) { "SecureMedia", "SecureMediaSessions" };
            foreach (var m in models)
            {
                var setName = m.ClassName + "s";
                for (int i = 2; !used.Add(setName); i++) setName = m.ClassName + "s" + i;
                sb.AppendLine($"    public DbSet<{m.ClassName}> {setName} => Set<{m.ClassName}>();");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildEntityAliases(IReadOnlyList<ModelInfo> models)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Lets NodeWalker logic refer to a table's class by the table name.");
            var classNames = new HashSet<string>(models.Select(m => m.ClassName), StringComparer.Ordinal) { "SecureMedia", "SecureMediaSession", "AppDbContext" };
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in models)
            {
                var table = m.QualifiedTableName?.Split('.').Last() ?? "";
                foreach (var alias in new[] { Pascal(table), table })
                {
                    if (!IsIdentifier(alias) || classNames.Contains(alias) || CSharpKeywords.Contains(alias)) continue;
                    if (!taken.Add(alias)) continue;
                    sb.AppendLine($"global using {alias} = {m.ClassName};");
                }
            }
            return sb.ToString();
        }


        private static string BuildController(string module, List<ApiFunction> functions, string scriptsDir, string logicDir, ref bool usesPariah)
        {
            var cls = Pascal(module) + "Controller";
            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine();
            sb.AppendLine("[ApiController]");
            sb.AppendLine($"[Route(\"api/{Slug(module)}\")]");
            sb.AppendLine($"public class {cls} : ControllerBase");
            sb.AppendLine("{");
            sb.AppendLine("    private static readonly HttpClient _http = new HttpClient();");
            sb.AppendLine("    private readonly AppDbContext _db;");
            sb.AppendLine($"    public {cls}(AppDbContext db) => _db = db;");
            sb.AppendLine();

            var methodNames = new HashSet<string>(StringComparer.Ordinal) { cls };
            foreach (var f in functions)
            {
                var verb = NormalizeVerb(f.Verb);
                var route = $"{Slug(f.Endpoint)}/{Slug(f.Function)}";
                var method = Pascal(f.Endpoint) + Pascal(f.Function);
                for (int i = 2; !methodNames.Add(method); i++) method = Pascal(f.Endpoint) + Pascal(f.Function) + i;

                var (parameters, body) = BuildAction(f, verb, scriptsDir, logicDir, ref usesPariah);

                if (!string.IsNullOrWhiteSpace(f.Description))
                    sb.AppendLine($"    /// <summary>{System.Security.SecurityElement.Escape(f.Description.Trim())}</summary>");
                sb.AppendLine($"    [Http{verb}(\"{route}\")]");
                sb.AppendLine($"    public async Task<IActionResult> {method}({string.Join(", ", parameters)})");
                sb.AppendLine("    {");
                foreach (var line in body) sb.AppendLine("        " + line);
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string SessionSlug(string module, string endpoint, string function) =>
            $"API_{SlugPart(module)}_{SlugPart(endpoint)}_{SlugPart(function)}";

        private static string SlugPart(string s) =>
            string.IsNullOrWhiteSpace(s) ? "untitled" :
            new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static (List<string> parameters, List<string> body) BuildAction(ApiFunction f, string verb, string scriptsDir, string logicDir, ref bool usesPariah)
        {
            var parameters = new List<string>();
            var stub = new List<string>
            {
                "// No NodeWalker graph found for this endpoint yet. Build one in the API Editor.",
                "await Task.CompletedTask;",
                "return Ok(new { message = \"Not implemented yet.\" });"
            };

            try
            {
                var slug = SessionSlug(f.Module, f.Endpoint, f.Function);
                var sessionFile = string.IsNullOrEmpty(scriptsDir) ? null : Path.Combine(scriptsDir, slug + ".json");
                if (sessionFile == null || !File.Exists(sessionFile))
                    return (parameters, stub);

                // Read synchronously: this runs on the UI thread during Build, and
                // blocking on LoadSession's async read there deadlocks the app.
                var session = NodeOperations.DeserializeSession(File.ReadAllText(sessionFile));
                if (session?.Nodes == null || session.Nodes.Count == 0) return (parameters, stub);

                Directory.CreateDirectory(logicDir);
                var script = NodeCompiler.CompileToScript(session);
                if (script.Contains("Pariah_Cybersecurity")) usesPariah = true;
                File.WriteAllText(Path.Combine(logicDir, slug + ".g.cs"), script);

                var e = NodeCompiler.DescribeEntry(session);
                var args = new List<string>();
                bool bodyUsed = false;
                bool bodyVerb = verb is "Post" or "Put" or "Patch";
                foreach (var (type, name) in NodeCompiler.DescribeParameters(session))
                {
                    if (name == "_db") { args.Add("_db"); continue; }
                    if (name == "_http") { args.Add("_http"); continue; }

                    bool complex = !SimpleTypes.Contains(type.TrimEnd('?'));
                    var source = bodyVerb && complex && !bodyUsed ? "[FromBody]" : "[FromQuery]";
                    if (source == "[FromBody]") bodyUsed = true;
                    var pname = "@" + name;
                    parameters.Add($"{source} {type} {pname}");
                    args.Add(pname);
                }

                var call = (e.IsAsync ? "await " : "") + $"{e.ClassName}.{e.Method}({string.Join(", ", args)})";
                var lines = new List<string> { $"// Logic compiled from the NodeWalker graph -> Controllers/Logic/{slug}.g.cs" };
                if (!e.IsAsync) lines.Add("await Task.CompletedTask;");
                lines.Add("try");
                lines.Add("{");
                if (e.ReturnsValue)
                {
                    lines.Add($"    var result = {call};");
                    lines.Add("    return Ok(result);");
                }
                else
                {
                    lines.Add($"    {call};");
                    lines.Add("    return Ok();");
                }
                lines.Add("}");
                lines.Add("// A \"Fail If\" block fired: tell the caller why (e.g. \"Out of stock\").");
                lines.Add("catch (InvalidOperationException ex) when (ex.Data.Contains(\"DD.FailIf\"))");
                lines.Add("{");
                lines.Add("    return BadRequest(new { error = ex.Message });");
                lines.Add("}");
                return (parameters, lines);
            }
            catch (Exception ex)
            {
                return (parameters, new List<string>
                {
                    $"// Failed to compile NodeWalker logic: {ex.Message.Replace('\n', ' ')}",
                    "await Task.CompletedTask;",
                    "return Problem(\"Logic generation failed; see the build log.\");"
                });
            }
        }


        private static readonly HashSet<string> SimpleTypes = new(StringComparer.Ordinal)
        {
            "string", "int", "long", "short", "double", "float", "decimal", "bool", "Guid",
            "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "object"
        };

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
            "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
            "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
            "internal","is","lock","long","namespace","new","null","object","operator","out","override",
            "params","private","protected","public","readonly","ref","return","sbyte","sealed","short",
            "sizeof","stackalloc","static","string","struct","switch","this","throw","true","try","typeof",
            "uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while"
        };

        private static bool IsIdentifier(string s) =>
            !string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_]*$");

        private static string NormalizeVerb(string verb) =>
            (verb ?? "GET").Trim().ToUpperInvariant() switch
            {
                "POST" => "Post",
                "PUT" => "Put",
                "DELETE" => "Delete",
                "PATCH" => "Patch",
                _ => "Get"
            };

        public static string Pascal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Unnamed";
            var parts = Regex.Split(s, @"[^A-Za-z0-9]+").Where(p => p.Length > 0);
            var result = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            if (result.Length == 0) return "Unnamed";
            return char.IsDigit(result[0]) ? "_" + result : result;
        }

        private static string Slug(string s)
        {
            var slug = Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return slug.Length == 0 ? "index" : slug;
        }

        private const string ProgramCs = @"using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(""Default"") ??
    ""Host=localhost;Database=yourdatabase;Username=postgres;Password=yourpassword"";
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(""v1"", new OpenApiInfo
    {
        Title = ""Database Designer API"",
        Version = ""v1"",
        Description = ""Auto-generated API from Database Designer""
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();
app.Run();
";

        private const string AppSettingsJson = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {
    ""Default"": ""Host=localhost;Database=yourdatabase;Username=postgres;Password=yourpassword""
  }
}";

        private const string CsProj = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>$(NoWarn);CS8600;CS8602;CS8603;CS8604;CS8618;CS8625;CS1998;CS0219;CS8981</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""8.0.11"" />
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""8.0.11"" />
    <PackageReference Include=""Dapper"" Version=""2.1.35"" />
    <PackageReference Include=""Microsoft.AspNetCore.OpenApi"" Version=""8.0.11"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.9.0"" />{PARIAH}
  </ItemGroup>
</Project>";
    }
}
