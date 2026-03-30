using System.Diagnostics;

namespace WhiskeyAndSmokes.Api;

/// <summary>
/// Centralized ActivitySource definitions for distributed tracing.
/// Each major subsystem gets its own source for granular control.
/// </summary>
public static class Diagnostics
{
    public const string ServiceName = "WhiskeyAndSmokes.Api";

    public static readonly ActivitySource General = new(ServiceName);
    public static readonly ActivitySource Auth = new($"{ServiceName}.Auth");
    public static readonly ActivitySource Captures = new($"{ServiceName}.Captures");
    public static readonly ActivitySource Agent = new($"{ServiceName}.Agent");
    public static readonly ActivitySource Storage = new($"{ServiceName}.Storage");
    public static readonly ActivitySource Admin = new($"{ServiceName}.Admin");

    public static readonly string[] AllSources =
    [
        ServiceName,
        $"{ServiceName}.Auth",
        $"{ServiceName}.Captures",
        $"{ServiceName}.Agent",
        $"{ServiceName}.Storage",
        $"{ServiceName}.Admin"
    ];
}
