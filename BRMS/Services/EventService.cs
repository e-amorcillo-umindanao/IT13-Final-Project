using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class EventService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;
    private readonly AuthService _authService;

    public EventService(AppDbContext dbContext, AuditService auditService, AuthService authService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authService = authService;
    }

    public async Task<List<Event>> GetAllEventsAsync()
    {
        return await BaseEventQuery()
            .AsNoTracking()
            .OrderByDescending(eventItem => eventItem.EventDate)
            .ToListAsync();
    }

    public async Task<Event?> GetEventByIdAsync(int id)
    {
        return await BaseEventQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(eventItem => eventItem.EventId == id);
    }

    public async Task<Event> CreateEventAsync(Event ev, int createdByUserId)
    {
        NormalizeEvent(ev);
        ev.CreatedAt = DateTime.UtcNow.ToString("O");
        ev.CreatedBy = createdByUserId;

        _dbContext.Events.Add(ev);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            createdByUserId,
            "Create",
            "Events",
            ev.EventId,
            $"Created event {ev.Title}.");

        return ev;
    }

    public async Task<bool> UpdateEventAsync(Event ev)
    {
        var existingEvent = await _dbContext.Events
            .FirstOrDefaultAsync(candidate => candidate.EventId == ev.EventId);

        if (existingEvent is null)
        {
            return false;
        }

        NormalizeEvent(ev);
        existingEvent.Title = ev.Title;
        existingEvent.Description = string.IsNullOrWhiteSpace(ev.Description) ? null : ev.Description.Trim();
        existingEvent.EventDate = ev.EventDate;
        existingEvent.Venue = string.IsNullOrWhiteSpace(ev.Venue) ? null : ev.Venue.Trim();
        existingEvent.EventType = ev.EventType;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            _authService.CurrentUser?.UserId ?? existingEvent.CreatedBy,
            "Update",
            "Events",
            existingEvent.EventId,
            $"Updated event {existingEvent.Title}.");

        return true;
    }

    public async Task<bool> DeleteEventAsync(int id)
    {
        var eventItem = await _dbContext.Events
            .Include(candidate => candidate.Attendances)
            .FirstOrDefaultAsync(candidate => candidate.EventId == id);

        if (eventItem is null)
        {
            return false;
        }

        if (eventItem.Attendances.Count > 0)
        {
            return false;
        }

        _dbContext.Events.Remove(eventItem);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            _authService.CurrentUser?.UserId ?? eventItem.CreatedBy,
            "Delete",
            "Events",
            eventItem.EventId,
            $"Deleted event {eventItem.Title}.");

        return true;
    }

    public async Task<List<Event>> GetUpcomingEventsAsync(int count = 5)
    {
        var today = DateTime.Today;
        var events = await BaseEventQuery()
            .AsNoTracking()
            .OrderBy(eventItem => eventItem.EventDate)
            .ToListAsync();

        return events
            .Where(eventItem => DateTime.TryParse(eventItem.EventDate, out var parsedDate) && parsedDate.Date >= today)
            .Take(count)
            .ToList();
    }

    private IQueryable<Event> BaseEventQuery()
    {
        return _dbContext.Events
            .Include(eventItem => eventItem.Attendances)
                .ThenInclude(attendance => attendance.Resident);
    }

    private static void NormalizeEvent(Event ev)
    {
        ev.Title = ev.Title.Trim();
        ev.Description = string.IsNullOrWhiteSpace(ev.Description) ? null : ev.Description.Trim();
        ev.EventDate = ev.EventDate.Trim();
        ev.Venue = string.IsNullOrWhiteSpace(ev.Venue) ? null : ev.Venue.Trim();
        ev.EventType = ev.EventType.Trim();
    }
}
