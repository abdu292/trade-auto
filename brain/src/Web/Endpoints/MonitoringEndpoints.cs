using Brain.Application.Common.Interfaces;

namespace Brain.Web.Endpoints;

public static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder group)
    {
        var monitoring = group.MapGroup("/monitoring").WithTags("Monitoring");

        monitoring.MapGet(
            "/ledger",
            (ITradeLedgerService ledger) => TypedResults.Ok(ledger.GetState()))
            .WithName("GetLedgerState")
            .WithDescription("Returns deterministic ledger state (cash, grams, exposure, deployable cash).");

        monitoring.MapGet(
            "/notifications",
            (INotificationFeedStore feedStore, int take = 50) => TypedResults.Ok(feedStore.GetLatest(take)))
            .WithName("GetNotificationFeed")
            .WithDescription("Returns mock outbound notifications (WhatsApp + mobile app feed).");

        return group;
    }
}
