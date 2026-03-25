using System.Globalization;
using BRMS.Data;
using BRMS.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Helpers;

public class CsvImportHelper
{
    private static readonly string[] ExpectedColumns =
    [
        "FirstName",
        "LastName",
        "MiddleName",
        "BirthDate",
        "Gender",
        "CivilStatus",
        "ContactNumber",
        "Address",
        "PurokName",
        "Status",
        "Categories",
        "ResidencySince"
    ];

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Inactive",
        "Transferred",
        "Deceased"
    };

    private readonly AppDbContext _dbContext;

    public CsvImportHelper(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(List<Resident> Residents, List<string> Errors)> ParseResidentsAsync(Stream csvStream)
    {
        if (csvStream.CanSeek)
        {
            csvStream.Position = 0;
        }

        var errors = new List<string>();
        var residents = new List<Resident>();
        var purokLookup = await _dbContext.Puroks
            .AsNoTracking()
            .ToDictionaryAsync(purok => NormalizeLookupValue(purok.Name), purok => purok.PurokId);

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
            PrepareHeaderForMatch = args => args.Header?.Trim() ?? string.Empty
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, configuration);

        if (!await csv.ReadAsync())
        {
            errors.Add("CSV is empty.");
            return (residents, errors);
        }

        csv.ReadHeader();

        var headerSet = csv.HeaderRecord?
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => header.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missingColumns = ExpectedColumns
            .Where(column => !headerSet.Contains(column))
            .ToList();

        if (missingColumns.Count > 0)
        {
            errors.Add($"Missing required column(s): {string.Join(", ", missingColumns)}.");
            return (residents, errors);
        }

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            rowNumber++;

            try
            {
                var firstName = GetRequiredField(csv, "FirstName");
                var lastName = GetRequiredField(csv, "LastName");
                var purokName = GetRequiredField(csv, "PurokName");
                var birthDate = NormalizeDate(GetRequiredField(csv, "BirthDate"), "BirthDate");
                var residencySince = NormalizeDate(GetRequiredField(csv, "ResidencySince"), "ResidencySince");
                var gender = NormalizeValue(GetRequiredField(csv, "Gender"));
                var status = NormalizeStatus(GetRequiredField(csv, "Status"));

                if (!purokLookup.TryGetValue(NormalizeLookupValue(purokName), out var purokId))
                {
                    throw new InvalidOperationException($"Unknown PurokName '{purokName}'.");
                }

                residents.Add(new Resident
                {
                    FirstName = NormalizeValue(firstName),
                    LastName = NormalizeValue(lastName),
                    MiddleName = NullIfWhiteSpace(csv.GetField("MiddleName")),
                    BirthDate = birthDate,
                    Gender = gender,
                    CivilStatus = NullIfWhiteSpace(csv.GetField("CivilStatus")),
                    ContactNumber = NullIfWhiteSpace(csv.GetField("ContactNumber")),
                    Address = NullIfWhiteSpace(csv.GetField("Address")),
                    PurokId = purokId,
                    Status = status,
                    Categories = NormalizeCategories(csv.GetField("Categories")),
                    ResidencySince = residencySince
                });
            }
            catch (Exception exception)
            {
                errors.Add($"Row {rowNumber}: {exception.Message}");
            }
        }

        return (residents, errors);
    }

    private static string GetRequiredField(CsvReader csv, string columnName)
    {
        var value = csv.GetField(columnName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{columnName} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeDate(string rawValue, string columnName)
    {
        var acceptedFormats = new[]
        {
            "yyyy-MM-dd",
            "M/d/yyyy",
            "MM/dd/yyyy",
            "M/d/yy",
            "MM/d/yyyy",
            "M/dd/yyyy",
            "yyyy/M/d",
            "yyyy/MM/dd",
            "MMM d, yyyy",
            "MMMM d, yyyy",
            "d MMM yyyy",
            "dd MMM yyyy"
        };

        if (DateTime.TryParseExact(rawValue, acceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExact) ||
            DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedExact) ||
            DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedExact))
        {
            return parsedExact.ToString("yyyy-MM-dd");
        }

        throw new InvalidOperationException($"{columnName} has an invalid date value '{rawValue}'.");
    }

    private static string NormalizeStatus(string rawValue)
    {
        var normalized = NormalizeValue(rawValue);
        if (!AllowedStatuses.Contains(normalized))
        {
            throw new InvalidOperationException($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        return AllowedStatuses.First(status => string.Equals(status, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCategories(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var categories = rawValue
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(", ", categories);
    }

    private static string NormalizeLookupValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeValue(string value)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
