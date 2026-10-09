# Contract fixtures, not package compatibility evidence

These projects deliberately build small metadata assemblies with the upstream assembly
names so the semantic recognizer can be tested independently of NuGet/runtime behavior.
They are test inputs only and are never referenced by the UO.Graphs product.

- `MediatR/`: unchanged API source files from MediatR tag `v12.4.1`;
  ISender, IPublisher, IRequestHandler, INotificationHandler, IPipelineBehavior.
  Source: https://github.com/LuckyPennySoftware/MediatR/tree/v12.4.1/src/MediatR
  Copyright Jimmy Bogard; Apache-2.0, LICENSE included. Actual
  MediatR.Contracts 2.0.1 supplies the markers and stream/unit types.
- `MassTransit/Contracts.cs`: reduced/adapted signatures, not the full upstream
  assembly. Based on tag `v8.3.6`, `src/MassTransit.Abstractions/IConsumer.cs`,
  `IPublishEndpoint.cs`, `ISendEndpoint.cs`, `Contexts/ConsumeContext.cs`.
  Source: https://github.com/MassTransit/MassTransit/tree/v8.3.6/src/MassTransit.Abstractions
  Copyright 2007-2024 Chris Patterson; Apache-2.0, LICENSE included.
  Pipe/context infrastructure and unrelated members intentionally omitted.

`MessagingTests.ContractFixturesAreSeparateFromRealPackageCompatibilityButGiveSameFlowShapes`
compares these fixtures with real NuGet metadata. The separate project
`../PackageCompatibility/PackageCompatibility.csproj` has actual MediatR 12.4.1 /
MassTransit.Abstractions 8.3.6 PackageReferences, no reference to these fixtures.
`../Uo/Uo.csproj` uses actual UO.Mediator.Shared 2.4.2. CLI integration acceptance
indexes those two package projects. None starts a host, mediator container or broker.
