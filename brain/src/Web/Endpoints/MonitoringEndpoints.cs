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

        monitoring.MapGet(
            "/approvals",
            IResult (ITradeApprovalStore approvals, int take = 20) => TypedResults.Ok(approvals.GetPending(take <= 0 ? 20 : take)))
            .WithName("GetApprovalQueue")
            .WithDescription("Returns manual approval queue. APPROVE moves trade to MT5 pending queue.");

        monitoring.MapPost(
            "/approvals/{tradeId:guid}/approve",
            IResult (Guid tradeId, ITradeApprovalStore approvals, IPendingTradeStore pendingTrades) =>
            {
                if (!approvals.TryApprove(tradeId, out var trade) || trade is null)
                {
                    return TypedResults.NotFound(new { message = "Trade not found in approval queue." });
                }

                pendingTrades.Enqueue(trade);
                return TypedResults.Ok(new { approved = true, tradeId = trade.Id });
            })
            .WithName("ApproveTrade")
            .WithDescription("Approves a queued trade and releases it to MT5 pending order pull endpoint.");

        monitoring.MapPost(
            "/approvals/{tradeId:guid}/reject",
            IResult (Guid tradeId, ITradeApprovalStore approvals) =>
            {
                var rejected = approvals.Reject(tradeId);
                if (!rejected)
                {
                    return TypedResults.NotFound(new { message = "Trade not found in approval queue." });
                }

                return TypedResults.Ok(new { rejected = true, tradeId });
            })
            .WithName("RejectTrade")
            .WithDescription("Rejects a queued trade and removes it from the manual approval queue.");

        return group;
    }
}
