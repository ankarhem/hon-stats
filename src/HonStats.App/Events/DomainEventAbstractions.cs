using HonStats.Domain.Events;

namespace HonStats.App.Events;

// Application-side plumbing. Domain owns the event contracts (IDomainEvent and
// the concrete events); App owns how they get dispatched and who handles them.
public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
