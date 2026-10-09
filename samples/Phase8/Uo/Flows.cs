using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using UO.Mediator.Dispatching;

namespace UoFlows;

public sealed record RequestA : IRequest<string>;
public sealed record RequestB : IRequest;
public sealed record Missing : IRequest;
public sealed record GenericRequest<T> : IRequest<T>;
public sealed class HandlerA(IRequestDispatcher dispatcher) : IRequestHandler<RequestA, string>
{
    public async Task<string> HandleAsync(RequestA request)
    {
        await dispatcher.DispatchAsync(new RequestB());
        return "done";
    }
}
public sealed class HandlerB : IRequestHandler<RequestB>
{
    public Task HandleAsync(RequestB request) => Task.CompletedTask;
}
public sealed class AlternativeB : IRequestHandler<RequestB>
{
    public Task HandleAsync(RequestB request) => Task.CompletedTask;
}
public sealed class IntHandler : IRequestHandler<GenericRequest<int>, int>
{
    public Task<int> HandleAsync(GenericRequest<int> request) => Task.FromResult(1);
}
public sealed class StringHandler : IRequestHandler<GenericRequest<string>, string>
{
    public Task<string> HandleAsync(GenericRequest<string> request) => Task.FromResult("value");
}
public sealed class AuditBehavior : IRequestBehavior<RequestA, string>
{
    public Task<string> HandleAsync(RequestA request, RequestHandlerNext<RequestA, string> next) => next.InvokeAsync();
}
public sealed class GenericBehavior<TRequest, TResponse> : IRequestBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, RequestHandlerNext<TRequest, TResponse> next) => next.InvokeAsync();
}
public sealed class Service(IRequestDispatcher dispatcher)
{
    public Task<string> Run() => dispatcher.DispatchAsync(new RequestA());
    public Task Unknown(IRequest request) => dispatcher.DispatchAsync(request);
    public Task Missing() => dispatcher.DispatchAsync(new Missing());
    public Task<int> Generic() => dispatcher.DispatchAsync(new GenericRequest<int>());
}
[Route("phase8")]
public sealed class FlowController(Service service) : ControllerBase
{
    [HttpGet("start")]
    public Task<string> Start() => service.Run();
}
public static class Registration
{
    public static void Configure(IServiceCollection services, bool enabled)
    {
        services.AddTransient<IRequestHandler<RequestA, string>, HandlerA>();
        services.AddTransient<IRequestHandler<RequestB>, HandlerB>();
        if (enabled) services.AddTransient<IRequestHandler<RequestB>, AlternativeB>();
    }
}
