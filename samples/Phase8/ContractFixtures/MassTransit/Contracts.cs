// Reduced contract fixture, adapted from MassTransit v8.3.6 (Apache-2.0).
// This is NOT the MassTransit NuGet assembly or a runtime implementation.
namespace MassTransit;
public interface IConsumer { }
public interface IConsumer<in TMessage> : IConsumer where TMessage : class
{
    Task Consume(ConsumeContext<TMessage> context);
}
public interface ConsumeContext<out T> where T : class { T Message { get; } }
public interface IPublishEndpoint
{
    Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class;
    Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class;
    Task Publish(object message, CancellationToken cancellationToken = default);
    Task Publish(object message, Type messageType, CancellationToken cancellationToken = default);
}
public interface ISendEndpoint
{
    Task Send<T>(T message, CancellationToken cancellationToken = default) where T : class;
    Task Send<T>(object values, CancellationToken cancellationToken = default) where T : class;
    Task Send(object message, CancellationToken cancellationToken = default);
    Task Send(object message, Type messageType, CancellationToken cancellationToken = default);
}
