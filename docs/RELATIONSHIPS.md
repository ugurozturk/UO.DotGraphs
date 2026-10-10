# Semantic relationship contract

The authoritative kind/direction table is `RelationshipContract.Definitions` in
`src/UO.Graphs.Core/Relationships.cs`. Current query JSON is **v5**, SQLite schema
**3**, and declaration identity **v2**. Phase 3 introduced the semantic edges;
its former JSON v3/SQLite v2 output is preserved only in historical records.

| Kind | Source → target | Static meaning |
| --- | --- | --- |
| DECLARES | Direct owner → declaration | Solution/project/assembly/namespace/type/member ownership; partial locations are separate occurrences |
| PROJECT_REFERENCES | Referencing project → referenced project | Evaluated MSBuild reference in selected context; missing/mismatched targets unresolved |
| INHERITS | Derived type → direct base class/interface | Bound written base list, including interface inheritance; not transitive closure |
| IMPLEMENTS | Implementing type/member → interface/member | Roslyn interface implementation, including inherited/explicit/default-member mappings |
| OVERRIDES | Overriding member → immediate overridden member | Bound method/property/event override |
| USES_TYPE | Containing declaration → bound type | Type syntax and creation evidence; generic arguments separately recorded |
| REFERENCES | Containing declaration → member | Field/property/event or method group use, not invocation |
| CALLS | Caller → compile-time target | Invocation, constructor, accessor, or supported operator binding |

`exact` proves these static meanings. Interface calls bind to interface slots;
virtual calls bind to the selected base/override slot. `base.M()` and virtual
`x.M()` retain different occurrence explanations. Implementations are not expanded
into exact CALLS. The semantic extractor produces no MAY_DISPATCH_TO expansion;
later framework extractors have separately labeled inferred candidates.

## Relationships, occurrences, and endpoints

- Relationship identity includes kind, source ID, target ID or empty value,
  project context, and confidence. Repeated caller/target uses share an edge.
- Occurrences retain relationship/analysis/context IDs, file, one-based line/column
  range with exclusive end, decoded-source SHA-256, generated flag, reason,
  `boundTarget`, and `relatedSymbols`.
- Two calls between the same methods remain two occurrences. Getter/setter uses at
  one span remain separate edges. Different unresolved calls by one owner can share
  a null-target edge but retain their own occurrence evidence. Candidate symbols
  are related symbols, never asserted targets.
- Generic/reduced extension targets normalize to original definitions; constructed
  call signatures stay in boundTarget. Partial calls normalize to the definition part.
- Endpoints retain declaration key, display name, assembly identity, and optional
  project/entity IDs. Metadata, implicit constructors, and excluded generated
  declarations do not become fake source entities. External IDs include the
  analyzing project context, assembly identity, and declaration key.
- Source-project endpoints require matching Roslyn compilation references, not
  names. Context mismatches remain separate reference symbols and report
  `relationshipContextMismatch` with partial coverage.
- DECLARES uses declaration spans, or project-file evidence for implicit namespace/
  assembly containers. PROJECT_REFERENCES uses evaluated items and project-file
  hash; `1:1–1:1` is explicitly not an exact XML element span. Calls use real syntax spans.
- Relationship IDs are location-independent. Occurrence IDs depend on source
  location/hash/reason and do not inherit portable declaration-ID guarantees.

## Semantic support matrix

| Pattern | Behavior | Limit |
| --- | --- | --- |
| Invocation/overload | Exact IInvocationOperation.TargetMethod | Candidate symbols never promoted |
| Generic call | OriginalDefinition edge, constructed occurrence | No runtime generic type analysis |
| Extension method | ReducedFrom/original static declaration | No receiver/name guessing |
| Creation, target-typed new, constructor initializer | Bound constructor/invocation CALLS | Implicit base span may be the declaration; synthesized lowering not exhaustive |
| Property/indexer read | REFERENCES + getter CALLS | Getter body belongs to its accessor caller |
| Write/compound write/increment | Setter; getter too for read/write | Ref-return write uses getter, no invented setter |
| Event add/remove | REFERENCES + accessor CALLS | Event invocation's delegate target unresolved |
| Method group/delegate creation | IMethodReferenceOperation REFERENCES | Conversion is not invocation |
| Delegate invocation | Unresolved CALLS, Invoke signature as related symbol | No assignment/data-flow/multicast resolution |
| Local function | Separate lexical declaration and semantic target | Equal names in different scopes remain distinct |
| Lambda/anonymous method | Separate caller for body calls | Runtime delegate invocation unresolved; expression-tree binding is not execution |
| Interface/virtual/explicit implementation | Compile-time CALLS, separate IMPLEMENTS/OVERRIDES | No runtime expansion |
| Partial declaration | Shared declaration, multiple source occurrences | Excluded/missing source stays incomplete |
| External call | Assembly/documentation-key endpoint | External body not analyzed |
| Dynamic invocation/creation/member | Unresolved CALLS/REFERENCES | No name-based target |
| Reflection/Delegate.DynamicInvoke | Exact API call plus unresolved runtime target | Recognizes MethodBase/MethodInfo.Invoke and DynamicInvoke; no universal reflection resolution |
| nameof | Member REFERENCES where applicable | No CALLS |
| User-defined binary/unary/conversion operator | IOperation.OperatorMethod CALLS | Other implicit protocols/lowering not exhaustive |
| Invalid/ambiguous call | Unresolved, reason and candidates | Recovery binding inside erroneous executable declarations not exact |

USES_TYPE is not whole-program type/data-flow analysis. Explicit type syntax,
Roslyn-bound `var`, and creation types are covered; no exhaustive closure over
implicit conversions/generic inference is promised. Parameters/local variables are
not standalone graph entities. Arrays/pointers reduce to element types.

No exhaustive call extraction for foreach/await/using disposal, pattern or
deconstruction lowering, function pointers, collection expressions, and synthesized
members. Ordinary explicit calls within those sources are still analyzed.
Absence outside this matrix is not evidence. Compilation errors remain diagnostic
and partial; valid independent members can still contribute exact relationships.

## Persistence and querying

Schema 3 atomically replaces analysis, entities, endpoints, relationships, and
occurrences, with foreign-key checks. Failures preserve the old snapshot/schema.
Unknown schemas are not overwritten; status can read supported legacy schemas,
but current queries require re-indexing schemas 1/2.

Index/status report relationshipCount and occurrenceCount. Current find returns
v5 query items, not the old Phase 3 adjacent relationship arrays. Use traversal
`items[].path[]`, context, or export for relationship/occurrence evidence. See
[QUERIES](QUERIES.md) and [CONTEXT-EXPORT](CONTEXT-EXPORT.md).

Tests include RelationshipTests, Phase3Tests, StorageTests, and workspace
regressions. Assertions compare full exact-target sets and forbidden connections,
not only counts. Actual executions are recorded in [STATUS](STATUS.md).

## Framework relationship extensions

| Area | Additional directions |
| --- | --- |
| DI/HTTP | Registration → service REGISTERS; registration → implementation IMPLEMENTED_BY; registration → factory USES_FACTORY; constructor/handler → parameter service INJECTS; endpoint → handler HANDLED_BY; action → constructor candidate ACTIVATES |
| Messaging | Handler → message HANDLES; consumer → message CONSUMES; caller → message DISPATCHES/PUBLISHES; message → candidate POSSIBLE_TARGET; behavior → message BEHAVIOR_FOR; message → behavior POSSIBLE_BEHAVIOR |
| ABP | Service → interface APP_SERVICE_CONTRACT; module → dependency MODULE_DEPENDS_ON; repository use/declaration → entity REPOSITORY_ENTITY |
| EF Core | Context/member → entity CONTEXT_ENTITY; configuration → entity CONFIGURES_ENTITY; mapping declaration → entity MAPS_ENTITY |
| Refit | Interface method → client contract HTTP_CONTRACT; caller → client contract INVOKES_HTTP; client contract → unresolved remote endpoint POSSIBLE_ENDPOINT |

These preserve source evidence and confidence. They do not change CALLS semantics.
ABP endpoint-candidate HANDLED_BY is inferred. Detailed boundaries are in
[FRAMEWORKS](FRAMEWORKS.md), [MESSAGING](MESSAGING.md), and
[ABP-EF-REFIT](ABP-EF-REFIT.md).
