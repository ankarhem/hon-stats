using HonStats.App.Events;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.Insights;

// Resolves all IEventHandler<TEvent> from a fresh DI scope per dispatch. Registered
// as a singleton holding the root provider; each dispatch spins a scope so scoped
// handlers (EF contexts, juvio clients) resolve cleanly.
internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    public async Task DispatchAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : HonStats.Domain.Events.IDomainEvent
    {
        using var scope = services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(@event, ct);
        }
    }
}
