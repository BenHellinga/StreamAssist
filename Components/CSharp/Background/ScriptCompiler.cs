using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using StreamAssist.Scripting;

namespace StreamAssist.Components.CSharp.Background;



// a compiled script: the class to make one of for each run, loaded in its own context so an older version can be let go
// once a newer one replaces it and nothing runs it any more
public sealed record CompiledScript(Type ScriptType, string FilePath, DateTime CompiledAt);



// a compile that failed: the first error, e.g. "line 12: ; expected", and every error (for the compile output)
public sealed record ScriptCompileError(string Message, IReadOnlyList<string> All)
{
    public ScriptCompileError(string message) : this(message, [message]) { }
}



// compiles one script file (plus the Shared folder's files) with Roslyn, the same way VS Code sees it through the
// generated project: the same implicit usings, StreamAssist.Scripting, nullable on. The run's checks are added first
// (ScriptGuardRewriter: loop limit, cancellation, recursion). Thread-safe (runs off the UI thread)
public static class ScriptCompiler
{
    // the usings every file gets, as the generated project gives them (ImplicitUsings + StreamAssist.Scripting)
    public static readonly IReadOnlyList<string> GlobalUsings =
    [
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading",
        "System.Threading.Tasks", "StreamAssist.Scripting",
    ];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    private const int MaxErrors = 20; // a missing brace can give hundreds, the first few say what's wrong



    // the file compiled into a loaded script class, or why not. sharedFiles are compiled into it too (helpers)
    public static (CompiledScript? Script, ScriptCompileError? Error) Compile(string filePath, IEnumerable<string> sharedFiles)
    {
        string source;
        try
        {
            source = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            return (null, new($"can't read file ({ex.Message})"));
        }

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(SourceText.From(string.Concat(GlobalUsings.Select(u => $"global using {u};\n")), Encoding.UTF8), options, "GlobalUsings.cs"),
            ScriptGuardRewriter.Rewrite(CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), options, filePath)),
        };
        foreach (var shared in sharedFiles)
        {
            try
            {
                trees.Add(ScriptGuardRewriter.Rewrite(CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(shared), Encoding.UTF8), options, shared)));
            }
            catch (Exception ex)
            {
                return (null, new($"can't read {Path.GetFileName(shared)} ({ex.Message})"));
            }
        }

        var compilation = CSharpCompilation.Create(
            $"Script_{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                optimizationLevel: OptimizationLevel.Debug));

        using var dll = new MemoryStream();
        using var pdb = new MemoryStream();
        var result = compilation.Emit(dll, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        if (!result.Success)
        {
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => Describe(d, filePath)).Distinct().Take(MaxErrors).ToList();
            return (null, new(errors[0], errors));
        }

        dll.Position = 0;
        pdb.Position = 0;
        var context = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true);
        var assembly = context.LoadFromStream(dll, pdb);

        var types = assembly.GetTypes().Where(t => typeof(StreamAssistScript).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        if (types.Count == 0)
        {
            context.Unload();
            return (null, new("no class that extends StreamAssistScript"));
        }
        if (types.Count > 1)
        {
            context.Unload();
            return (null, new($"more than one class extends StreamAssistScript ({string.Join(", ", types.Select(t => t.Name))})"));
        }
        if (types[0].GetConstructor(Type.EmptyTypes) == null)
        {
            context.Unload();
            return (null, new($"{types[0].Name} needs a constructor with no parameters"));
        }

        return (new(types[0], filePath, DateTime.Now), null);
    }



    // e.g. "line 12: ; expected", or "Helpers.cs line 3: ..." for a shared file
    private static string Describe(Diagnostic error, string filePath)
    {
        var span = error.Location.GetLineSpan();
        var where = span.Path == filePath || string.IsNullOrEmpty(span.Path) ? "" : $"{Path.GetFileName(span.Path)} ";
        return $"{where}line {span.StartLinePosition.Line + 1}: {error.GetMessage()}";
    }



    // the .NET assemblies this app runs on (what the generated project's SDK gives VS Code) and the scripting API
    private static IReadOnlyList<MetadataReference> LoadReferences()
    {
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p =>
            {
                var name = Path.GetFileNameWithoutExtension(p);
                return name.StartsWith("System.") || name.StartsWith("Microsoft.CSharp") || name is "System" or "mscorlib" or "netstandard";
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        paths.Add(typeof(StreamAssistScript).Assembly.Location);

        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }



    // the line in the script a runtime error came from, e.g. " (line 12)", or "" if it isn't known
    public static string LineOf(Exception ex, string filePath)
    {
        var trace = new System.Diagnostics.StackTrace(ex, true);
        foreach (var frame in trace.GetFrames())
            if (frame.GetFileName() is { } file && string.Equals(Path.GetFullPath(file), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase) && frame.GetFileLineNumber() > 0)
                return $" (line {frame.GetFileLineNumber()})";
        return "";
    }
}
