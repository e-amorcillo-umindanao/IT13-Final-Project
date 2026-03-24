using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class EventService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public EventService(AppDbContext db, AuthService authService, AuditService auditService)
    {
        _db = db;
        _authService = authService;
        _auditService = auditService;
    }

    public Task<List<Event>> GetAllAsync()
    {
        return _db.Events
            .AsNoTracking()
            .Include(eventItem => eventItem.Attendances)
            .ThenInclude(attendance => attendance.Member)
            .OrderByDescending(eventItem => eventItem.EventDate)
            .ToListAsync();
    }

    public Task<Event?> GetByIdAsync(int id)
    {
        return _db.Events
            .AsNoTracking()
            .Include(eventItem => eventItem.Attendances)
            .ThenInclude(attendance => attendance.Member)
            .FirstOrDefaultAsync(eventItem => eventItem.EventId == id);
    }

    public Task<List<Event>> GetUpcomingAsync(int count = 5)
    {
        return _db.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.EventDate >= DateTime.Today)
            .OrderBy(eventItem => eventItem.EventDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task CreateAsync(Event ev)
    {
        Normalize(ev);

        var currentUserId = GetCurrentUserId();
        ev.CreatedAt = DateTime.UtcNow;
        ev.CreatedBy = currentUserId;

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "CREATE_EVENT",
            "Events",
            ev.EventId,
            $"Created event {ev.Title} on {ev.EventDate:yyyy-MM-dd}.");
    }

    public async Task UpdateAsync(Event ev)
    {
        Normalize(ev);

        var existingEvent = await _db.Events.FirstOrDefaultAsync(eventItem => eventItem.EventId == ev.EventId);
        if (existingEvent is null)
        {
            return;
        }

        existingEvent.Title = ev.Title;
        existingEvent.EventType = ev.EventType;
        existingEvent.EventDate = ev.EventDate;
        existingEvent.Venue = ev.Venue;
        existingEvent.Description = ev.Description;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UPDATE_EVENT",
            "Events",
            existingEvent.EventId,
            $"Updated event {existingEvent.Title}.");
    }

    public async Task DeleteAsync(int id)
    {
        var eventItem = await _db.Events.FirstOrDefaultAsync(item => item.EventId == id);
        if (eventItem is null)
        {
            return;
        }

        var eventTitle = eventItem.Title;

        _db.Events.Remove(eventItem);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "DELETE_EVENT",
            "Events",
            id,
            $"Deleted event {eventTitle}.");
    }

    private int GetCurrentUserId()
        => _authService.CurrentUser?.UserId
           ?? throw new InvalidOperationException("You must be logged in to manage events.");

    private static void Normalize(Event ev)
    {
        ev.Title = ev.Title.Trim();
        ev.EventType = string.IsNullOrWhiteSpace(ev.EventType) ? "General Assembly" : ev.EventType.Trim();
        ev.Venue = NormalizeOptional(ev.Venue);
        ev.Description = NormalizeOptional(ev.Description);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
