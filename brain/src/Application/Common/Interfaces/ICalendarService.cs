namespace Brain.Application.Common.Interfaces;

public interface ICalendarService
{
    Task ScheduleEventAsync(string title, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}
