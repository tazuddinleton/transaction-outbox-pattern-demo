# Design Decisions

## Unified Domain Event Model

**Single Event Model**: All events inherit from `DomainEvent` (abstract record) containing `EventId`, `OccurredOn`, and `RoutingKey`, eliminating the distinction between domain events and integration events. Since events in this system are exclusively published externally via the outbox pattern, they must include routing information from creation. Entities track `List<DomainEvent>` directly, ensuring events are ready for message broker publishing without transformation or conversion steps.

