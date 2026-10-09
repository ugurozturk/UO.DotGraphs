using System.Reflection;

namespace UO.Graphs.Core;

public static class ProductVersion
{
    // The SDK appends the source revision after '+'. Keep the NuGet version in CLI/graph output.
    public static string Current { get; } = typeof(ProductVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
