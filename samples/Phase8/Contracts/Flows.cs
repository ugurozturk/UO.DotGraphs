using MediatR;
using MassTransit;

namespace ContractFlows
{
    public sealed record Query : IRequest<string>;
    public sealed record Command : IRequest;
    public sealed record Event : INotification;
    public sealed record Unseen;
    public interface IEvent { }
    public sealed class QueryHandler : IRequestHandler<Query, string>
    {
        public Task<string> Handle(Query request, CancellationToken cancellationToken) => Task.FromResult("ok");
    }
    public sealed class CommandHandler : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public sealed class First : INotificationHandler<Event>
    {
        public Task Handle(Event notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public sealed class Second : INotificationHandler<Event>
    {
        public Task Handle(Event notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public sealed class Behavior : IPipelineBehavior<Query, string>
    {
        public Task<string> Handle(Query request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken) => next();
    }
    public sealed class Consumer1 : IConsumer<Event>
    {
        public Task Consume(ConsumeContext<Event> context) => Task.CompletedTask;
    }
    public sealed class Consumer2 : IConsumer<Event>
    {
        public Task Consume(ConsumeContext<Event> context) => Task.CompletedTask;
    }
    public sealed class OtherConsumer : IConsumer<Other.Event>
    {
        public Task Consume(ConsumeContext<Other.Event> context) => Task.CompletedTask;
    }
    public sealed class InterfaceConsumer : IConsumer<IEvent>
    {
        public Task Consume(ConsumeContext<IEvent> context) => Task.CompletedTask;
    }
    public static class Dispatch
    {
        public static async Task Run(ISender sender, IPublisher publisher, IPublishEndpoint bus, ISendEndpoint endpoint)
        {
            await sender.Send(new Query());
            await sender.Send(new Command());
            await publisher.Publish(new Event());
            await bus.Publish(new Event());
            await bus.Publish(new Other.Event());
            await bus.Publish(new Unseen());
            await bus.Publish<IEvent>(new { });
            await endpoint.Send(new Event());
        }
        public static async Task Unknown(ISender sender, IPublisher publisher, IPublishEndpoint bus, object value, Type type)
        {
            await sender.Send(value);
            await publisher.Publish(value);
            await bus.Publish(value, type);
        }
        public static Task Explicit(IPublishEndpoint bus, object value) => bus.Publish(value, typeof(Event));
    }
    public sealed class Lookalike
    {
        public Task Send(Event value) => Task.CompletedTask;
        public Task Publish(Event value) => Task.CompletedTask;
        public async Task Run() { await Send(new Event()); await Publish(new Event()); }
    }
}
namespace Other { public sealed record Event; }
