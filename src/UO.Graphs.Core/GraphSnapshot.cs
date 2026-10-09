using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UO.Graphs.Core;

public enum Confidence { Exact, Inferred, Unresolved }

public sealed record SourceEvidence(string File, int StartLine, int StartColumn,
    int EndLine, int EndColumn, string Sha256, bool Generated = false);

public sealed record GraphEntity(string Id, string Kind, string Name, string QualifiedName,
    string DocumentationId, string ProjectId, Confidence Confidence, SourceEvidence[] Sources,
    string CanonicalIdentity = "", string? ParentId = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RegistrationDeclaration? Registration { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HttpEndpointDeclaration? HttpEndpoint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageDeclaration? Message { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrameworkDeclaration? Framework { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelMappingDeclaration? ModelMapping { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HttpContractDeclaration? HttpContract { get; init; }
}

public sealed record FrameworkDeclaration(string Name, string Role, string ApiAssembly, string[] Limitations);

// A source mapping declaration, never the effective runtime model or a physical database object.
public sealed record ModelMappingDeclaration(string Form, string? EntityType, string? Member,
    string? Table, string? Schema, string? Column, bool Conditional, string[] Limitations, string? View = null);

public sealed record HttpContractDeclaration(string Framework, string? HttpMethod, string? Route,
    string? BaseAddress, string ContractMethod, string ApiAssembly, string[] Limitations);

public sealed record MessageDeclaration(string Framework, string ContractIdentity, string AssemblyIdentity,
    string ConstructedType, string Scope, string[] Limitations);

public sealed record RegistrationDeclaration(string Lifetime, string Form, bool Conditional,
    string? ServiceType, string? ImplementationType, bool OpenGeneric, string[] Limitations);

public sealed record HttpEndpointDeclaration(string? HttpMethod, string? RoutePattern,
    string? CollisionKey, string Framework, bool Conditional, string[] Limitations,
    string Activation = "notProven");

public sealed record ReferenceContext(string Path, string OutputPath, string[] PreprocessorSymbols);

public sealed record ProjectContext(string Id, string Path, string AssemblyName,
    string TargetFramework, string Configuration, string SdkVersion, string LanguageVersion,
    string[] PreprocessorSymbols, string MSBuildPath = "", string MSBuildVersion = "",
    string[]? SkippedTargetFrameworks = null, string[]? ProjectReferences = null,
    string[]? AnalyzerReferences = null, ReferenceContext[]? ReferenceContexts = null);

public sealed record AnalysisSnapshot(string Id, DateTimeOffset CreatedAtUtc, string InputPath,
    string ToolVersion, string RoslynVersion, string Coverage, string Freshness,
    string[] Limitations, string[] Diagnostics, ProjectContext[] Projects,
    string IdentityVersion = "1", string RootPath = "", string GeneratedPolicy = "exclude",
    bool GeneratorsAllowed = false)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FreshnessCheck? FreshnessCheck { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DependencyFingerprintSummary? DependencyFingerprint { get; init; }
}

public sealed record GraphSnapshot(AnalysisSnapshot Analysis, GraphEntity[] Entities)
{
    public IndexFingerprint? Fingerprint { get; init; }
    [JsonIgnore]
    public Dictionary<string, string> ObservedSources { get; init; } = new(StringComparer.Ordinal);
    public GraphRelationship[] Relationships { get; init; } = [];
    public RelationshipOccurrence[] Occurrences { get; init; } = [];
}

public sealed record IndexOptions(string Configuration = "Debug", string? Framework = null,
    string? Root = null, bool AllowGenerators = false, bool IncludeGenerated = false);

public static class GraphIdentity
{
    public const string Version = "2";
    // Length-prefixed UTF-8 fields avoid delimiter ambiguity. Paths must be root-relative.
    public static string Canonical(params string[] parts) => "uographs:2:" +
        string.Concat(parts.Select(p => $"{Encoding.UTF8.GetByteCount(p)}:{p}"));

    public static string Project(string relativePath, string framework, string configuration) =>
        Hash(ProjectCanonical(relativePath, framework, configuration));

    public static string ProjectCanonical(string relativePath, string framework, string configuration)
    {
        if (Path.IsPathRooted(relativePath)) throw new ArgumentException("Identity requires a root-relative project path.", nameof(relativePath));
        return Canonical("project", relativePath.Replace('\\', '/'), framework, configuration);
    }

    public static string Symbol(string projectId, string documentationId) =>
        Hash(Canonical("symbol", projectId, documentationId));

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class GraphJson
{
    public const int ContractVersion = 4;
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<RelationshipKind>(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public sealed class GraphException(int exitCode, string message) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}
