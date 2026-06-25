namespace HonStats.Domain.Events;

// Marker for domain events raised by the application. The events themselves are
// Domain contracts (they describe things that happened in the domain); the
// dispatcher and handler abstractions live in the App layer.
public interface IDomainEvent;
