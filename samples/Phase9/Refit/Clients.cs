using Microsoft.AspNetCore.Mvc;
using Refit;

namespace RefitFixture;
public interface IFirstApi
{
    [Get("/shared/{id}")] Task<string> GetAsync(int id);
    [Post("/shared")] Task<string> SaveAsync([Body] string value);
}
public interface ISecondApi { [Get("/shared/{id}")] Task<string> GetAsync(int id); }
public interface IInheritedApi : IFirstApi { }
public interface IShadowApi : IFirstApi { [Post("/other/{id}")] new Task<string> GetAsync(int id); }
public interface IDefaultApi
{
    [Get("/default")] Task<string> DefaultAsync() => Task.FromResult("local");
    static Task<string> StaticAsync() => Task.FromResult("local");
}
public class CustomGetAttribute(string path) : GetAttribute(path)
{
    public override string Path => "/custom/" + Environment.GetEnvironmentVariable("ROUTE");
}
public interface ICustomApi { [CustomGet("/ignored")] Task<string> Run(); }
public static class Caller
{
    public static Task<string> First(IFirstApi api) => api.GetAsync(1);
    public static Task<string> Second(ISecondApi api) => api.GetAsync(2);
    public static Task<string> Inherited(IInheritedApi api) => api.GetAsync(3);
    public static Task<string> Shadow(IShadowApi api) => api.GetAsync(4);
    public static IFirstApi Runtime(string address) => RestService.For<IFirstApi>(address);
    public static ISecondApi Client(HttpClient client) => RestService.For<ISecondApi>(client);
}
[ApiController]
public class LocalController : ControllerBase
{
    [HttpGet("/shared/{id}")] public string Get(int id) => id.ToString();
}
// Unrelated attribute with identical simple name must not become a Refit contract.
public class GetAttributeLookalike(string path) : Attribute { public string Path { get; } = path; }
public interface IFakeApi { [GetAttributeLookalike("/shared/{id}")] Task<string> Get(); }
