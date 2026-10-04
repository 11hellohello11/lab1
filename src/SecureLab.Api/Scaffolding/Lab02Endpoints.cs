using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            if (sortBy is not null and not "" and not "createdAtUtc" and not "severity" and not "status")
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["sortBy"] = ["Allowed values: createdAtUtc, severity, status."]
                    });
            }

            var order = sortBy switch
            {
                "severity" => "severity",
                "status" => "status",
                _ => "created_at_utc DESC"
            };

            var escapedQ = (q ?? "")
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedQ}%";

            var query = db.Incidents
                .AsNoTracking()
                .Where(row =>
                    EF.Functions.ILike(row.Title, pattern, @"\") ||
                    EF.Functions.ILike(row.Description, pattern, @"\"));

            query = order switch
            {
                "severity" => query
                    .OrderBy(row => row.Severity == IncidentSeverity.Critical ? 0 :
                                    row.Severity == IncidentSeverity.High ? 1 :
                                    row.Severity == IncidentSeverity.Medium ? 2 : 3)
                    .ThenBy(row => row.Id),

                "status" => query
                    .OrderBy(row => row.Status == IncidentStatus.New ? 0 :
                                    row.Status == IncidentStatus.Triaged ? 1 :
                                    row.Status == IncidentStatus.InProgress ? 2 :
                                    row.Status == IncidentStatus.Resolved ? 3 : 4)
                    .ThenBy(row => row.Id),

                _ => query
                    .OrderByDescending(row => row.CreatedAtUtc)
                    .ThenBy(row => row.Id)
            };

            var rows = await query
                .Take(50)
                .ToListAsync(ct);
            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            var title = request.Title?.Trim() ?? "";
            var description = request.Description?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(title))
            {
                errors["title"] = ["Title is required."];
            }
            else if (request.Title!.Length > 160)
            {
                errors["title"] = ["Title must not exceed 160 characters."];
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors["description"] = ["Description is required."];
            }
            else if (request.Description!.Length > 4000)
            {
                errors["description"] = ["Description must not exceed 4000 characters."];
            }

            var severityIsValid =
                Enum.TryParse<IncidentSeverity>(
                    request.Severity,
                    ignoreCase: true,
                    out var severity)
                && Enum.IsDefined(severity);

            if (!severityIsValid)
            {
                errors["severity"] =
                    ["Allowed values: Low, Medium, High, Critical."];
            }

            if (request.OccurredAtUtc is null)
            {
                errors["occurredAtUtc"] = ["OccurredAtUtc is required."];
            }
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] =
                    ["OccurredAtUtc must not be more than 5 minutes in the future."];
            }

            if (severityIsValid
                && (severity == IncidentSeverity.High ||
                    severity == IncidentSeverity.Critical)
                && description.Length < 40)
            {
                errors["description"] =
                    ["Description must contain at least 40 characters for High or Critical severity."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }
            var hasConflict = await db.Incidents.AnyAsync(
    item =>
        item.Title == title &&
        item.Status != IncidentStatus.Closed,
    ct);

            if (hasConflict)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Incident title conflict",
                    Detail = "An active incident with the same title already exists.",
                    Status = StatusCodes.Status409Conflict
                });
            }
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = DbSeeder.AliceId,
                Title = title,
                Description = description,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);
            return Results.Created(
                $"/api/incidents/{incident.Id}",
                new CreatedIncidentResponse(
                    incident.Id,
                    incident.Title,
                    incident.Description,
                    incident.Severity.ToString(),
                    incident.Status.ToString(),
                    incident.OccurredAtUtc,
                    incident.CreatedAtUtc,
                    incident.UpdatedAtUtc));
        });
    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
