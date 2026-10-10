# Canonical identity v2

Declaration IDs are independent of snapshot, process, line number, and absolute
checkout path. `analysis.identityVersion = "2"`; entities retain their
`canonicalIdentity`. The ID is the lowercase hexadecimal SHA-256 of the canonical
string's BOM-free UTF-8 bytes.

```text
C(parts...) = "uographs:2:" + concat(UTF8ByteCount(part) + ":" + part)
C("é", "a:b") = "uographs:2:2:é3:a:b"
```

Lengths are decimal. Unicode is not normalized; case is preserved. Length prefixes
avoid delimiter collisions. SHA-256 is not claimed to have zero theoretical
collision probability.

| Entity | Canonical input |
| --- | --- |
| project | `C("project", relativeProjectPath, targetFramework, configuration)` |
| solution | `C("solution", relativeInputPath, configuration, selectedFrameworkOrDefault)` |
| assembly | `C("symbol", projectId, "A:" + RoslynAssemblyIdentity)` |
| namespace/type/member | `C("symbol", projectId, declarationKey)` |

Project paths are relative to `analysis.rootPath`, with backslashes normalized to
`/`. An explicit `--root` must contain the input and all projects. The default is
the common ancestor of the input directory and reached projects. Relocating the
same layout with the same options preserves IDs. Use the same logical root when
comparing different input subsets. This is not global multi-repository federation.

## Declaration keys

The normal key is Roslyn's documentation comment ID:

- Namespace: `N:Fixture`; global namespace: `N:`. Project/TFM context separates them.
- Generic arity: ``T:Fixture.Box`` versus ``T:Fixture.Box`1``; nested types retain
  their own arity, such as ``T:Fixture.Box`1.Nested`1``.
- Overloads include parameter types, for example
  ``M:Fixture.Box`1.Run(System.Int32)`` versus `Run(System.String)`.
- Generic method arity and type-parameter ordinals follow Roslyn's format.
- Constructors `.ctor/.cctor`, indexer/ref parameters, explicit interface names
  with `#` escaping, operator metadata names, and conversion return types remain
  distinct. Extension methods use the static declaration and first parameter.
- Accessors use `accessor:<owner documentation ID>:<Roslyn MethodKind>`. Get/set
  and event add/remove are distinct. `init` is Roslyn's `PropertySet`; changing
  set to init alone does not create a new ID.
- File-local types/members use
  `C("file-local", relativeSourcePath, documentationId)`.
- Local functions use `C("local", containingDeclarationKey, relativeSourcePath,
  syntaxChildNodeOrdinalPath, fullSignature)`. The ordinal path separates equal
  names/signatures in distinct lexical scopes without using trivia or line numbers.
  Rearranging syntax or preceding siblings may change it; blank lines do not.
- Lambdas/anonymous methods use the same lexical mechanism with a `"lambda"`
  discriminator. Type-parameter endpoints include owner declaration key,
  TypeParameterKind, and ordinal.

Partial types/methods share one declaration key and preserve all indexed locations
in `sources`; source hashes do not participate in declaration identity.
Auto/expression-bodied property/event accessors use owner evidence; explicit
accessors also have their own source location. Implicit constructors, backing
fields, and compiler-synthesized record members are not emitted as source
entities. Written primary-constructor parameter lists produce constructor entities.

## Changes that affect identity

Renames and signature edits that change Roslyn's documentation ID, such as parameter
type or arity, create new IDs. Type/namespace renames also change member IDs.
Parameter names, generic parameter names, whitespace, and ordinary method return
types leave IDs unchanged when the documentation ID is unchanged; conversion
return types are an exception.

Changing relative project path, TFM, or configuration changes IDs throughout that
context. Assembly renaming changes the assembly ID; source declaration IDs remain
owned by project context. Evidence paths/lines can be absolute and depend on the
analyzed source; they are evidence, not identity.

## Versions and specialized records

Current query contract is **JSON v5**, SQLite schema **3**, and declaration identity
**2**. These are separate versions. Identity v2 was introduced in Phase 2; the
Phase 3 JSON v3/SQLite v2 combination is historical, not the current storage format.
Old snapshots retain identity version 1. Re-indexing creates new v2 records
atomically and never reinterprets old IDs. Freshness defaults to `notChecked`.

Relationship, occurrence, and external endpoint identities follow
[RELATIONSHIPS](RELATIONSHIPS.md). Occurrence IDs depend on source evidence and do
not inherit declaration relocation guarantees. Specialized registration/endpoint,
closed-message, and framework records have their own canonical inputs documented
in [FRAMEWORKS](FRAMEWORKS.md), [MESSAGING](MESSAGING.md), and
[ABP-EF-REFIT](ABP-EF-REFIT.md).
