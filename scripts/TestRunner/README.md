# Socket-free local fallback

Development verification only; not shipped with uographs. This runner invokes the
repository's existing xUnit assertions without VSTest's TCP communication socket.
It supports parameterless test classes, Fact, InlineData, Task and IDisposable,
creates a fresh class per case, rejects unsupported fixture/data/skip behavior,
and writes one JSON result per actual case. It is **not** a replacement for the
standard xUnit/VSTest runner, discovery or parallel-execution acceptance.

```sh
dotnet restore scripts/TestRunner/TestRunner.csproj --locked-mode --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false
dotnet build scripts/TestRunner/TestRunner.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet scripts/TestRunner/bin/Debug/net10.0/TestRunner.dll UnitTests .uographs/unit.json
```

A substring filter is the first argument. Empty filter attempts all cases,
including CLI tests which need MSBuild build-host IPC inside child processes.
No fixture, host, database or HTTP runtime is started by unit tests. ABP packages
are read as compiler metadata from their independently restored fixture assets;
there is no ABP project/package reference in this runner or test closure.

Use the standard `dotnet test` in an environment permitting VSTest IPC. Failures of
that path remain failures/NOT RUN in STATUS even if fallback assertions pass.
