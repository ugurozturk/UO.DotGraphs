using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FrameworkFixture;

public interface IMessage { string Read(); }
public sealed class Message : IMessage { public string Read() => "first"; }
public sealed class OtherMessage : IMessage { public string Read() => "other"; }
public interface IRepository<T> { }
public sealed class Repository<T> : IRepository<T> { }
public sealed class Consumer(IMessage message, IEnumerable<IMessage> all, IRepository<string> repository)
{
    public string Read() => message.Read() + all.Count() + repository.GetType().Name;
}

// This library is compiled and indexed, never executed. No host/container is started.
public static class Setup
{
    public static IServiceCollection Register(bool enabled, Type dynamicType)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessage, Message>();
        services.AddScoped<IMessage, OtherMessage>();
        services.AddTransient<Consumer>();
        services.AddSingleton<IMessage>(sp => new Message());
        services.AddTransient<IMessage>(Factory);
        services.AddSingleton<IMessage>(new Message());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        if (enabled) services.AddTransient<IMessage, OtherMessage>();
        services.AddScoped(typeof(IMessage), dynamicType);
        services.Scan();
        return services;
    }
    private static IMessage Factory(IServiceProvider provider) => new OtherMessage();
    private static IServiceCollection Scan(this IServiceCollection services) => services;
    public static string Handle([FromServices] IMessage message) => message.Read();
    public static void DuplicateRoutes(WebApplication app) => app.MapGet("/duplicate", () => "duplicate");
    public static void Routes(WebApplication app, string runtimeRoute, bool enabled)
    {
        var api = app.MapGroup("/api");
        var v1 = api.MapGroup("/v1");
        v1.MapGet("/messages", Handle);
        v1.MapPost("/messages", ([FromServices] IMessage message) => message.Read());
        app.MapMethods("/multi", new[] { "GET", "POST" }, Handle);
        app.MapMethods("/collection", ["PUT", "PATCH"], Handle);
        app.MapGet("/duplicate", Handle);
        DuplicateRoutes(app);
        app.MapPost("/duplicate", Handle);
        app.MapGet(runtimeRoute, Handle);
        app.MapGroup(runtimeRoute).MapGet("/child", Handle);
        if (enabled) app.MapGet("/conditional", Handle);
        Func<string> delegateValue = () => "value";
        app.MapGet("/delegate", delegateValue);
        var changed = app.MapGroup("/old");
        changed = app.MapGroup("/new");
        changed.MapGet("/uncertain", Handle);
        app.MapGet("/implicit", (IMessage message) => message.Read());
    }
}

[ApiController]
[Route("api/[controller]")]
public sealed class MessagesController(IMessage message) : ControllerBase
{
    [HttpGet("{id}")]
    public string Get(int id) => message.Read() + id;
    [HttpPost("{id}")]
    public string Post(int id, [FromServices] Consumer consumer) => consumer.Read() + id;
    [HttpGet("/absolute")]
    public string Absolute() => message.Read();
    [HttpGet, Route("aliases/a"), Route("aliases/b")]
    public string Aliases() => message.Read();
    [NonAction, HttpGet("hidden")]
    public string Hidden() => "hidden";
}

// Name collisions are intentionally ordinary source methods, never framework registrations/endpoints.
public static class Lookalikes
{
    public static void AddSingleton<T, U>() { }
    public static void MapGet(string path, Func<string> handler) { }
    public static void Use() { AddSingleton<IMessage, Message>(); MapGet("/fake", () => "fake"); }
}
