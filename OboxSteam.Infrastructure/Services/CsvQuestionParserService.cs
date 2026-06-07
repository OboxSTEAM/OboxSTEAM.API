using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;

namespace OboxSteam.Infrastructure.Services;

public sealed class CsvQuestionParserService : ICsvQuestionParserService
{
    private static readonly string[] RequiredHeaders =
    [
        "QuestionText",
        "QuestionType",
        "Difficulty",
        "Points"
    ];

    private static readonly Regex OptionHeaderRegex = new(
        "^Option(\\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IsCorrectHeaderRegex = new(
        "^IsCorrect(\\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<IReadOnlyList<CsvBankQuestionRowDto>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        if (csvStream == null)
            throw ErrorHelper.BadRequest("CSV stream is required.");

        using var reader = new StreamReader(
            csvStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
            throw ErrorHelper.BadRequest("CSV file is empty.");

        csv.ReadHeader();
        var headers = csv.HeaderRecord;
        if (headers == null || headers.Length == 0)
            throw ErrorHelper.BadRequest("CSV file must contain a header row.");

        var headerMap = BuildHeaderMap(headers);
        ValidateRequiredHeaders(headerMap);
        var optionPairs = BuildOptionPairs(headerMap);

        if (optionPairs.Count == 0)
            throw ErrorHelper.BadRequest("CSV must contain at least one Option/IsCorrect column pair.");

        var rows = new List<CsvBankQuestionRowDto>();

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsEmptyRow(csv, headerMap, optionPairs))
                continue;

            var row = new CsvBankQuestionRowDto
            {
                RowNumber = csv.Context.Parser!.Row,
                QuestionText = GetField(csv, headerMap, "QuestionText"),
                QuestionType = GetField(csv, headerMap, "QuestionType"),
                Difficulty = GetField(csv, headerMap, "Difficulty"),
                Points = ParsePoints(GetField(csv, headerMap, "Points"))
            };

            foreach (var pair in optionPairs)
            {
                var optionText = GetFieldByIndex(csv, pair.OptionIndex);
                if (string.IsNullOrWhiteSpace(optionText))
                    continue;

                var isCorrectRaw = GetFieldByIndex(csv, pair.IsCorrectIndex);
                if (!TryParseIsCorrect(isCorrectRaw, out var isCorrect))
                {
                    row.ParseErrors.Add(
                        $"Invalid IsCorrect value '{isCorrectRaw}' for option '{optionText}'.");
                    continue;
                }

                row.Options.Add(new CsvBankQuestionOptionRowDto
                {
                    OptionText = optionText,
                    IsCorrect = isCorrect
                });
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
            throw ErrorHelper.BadRequest("CSV file contains no question rows.");

        return Task.FromResult<IReadOnlyList<CsvBankQuestionRowDto>>(rows);
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i]?.Trim();
            if (string.IsNullOrWhiteSpace(header))
                continue;

            map.TryAdd(header, i);
        }

        return map;
    }

    private static void ValidateRequiredHeaders(Dictionary<string, int> headerMap)
    {
        var missing = RequiredHeaders
            .Where(h => !headerMap.ContainsKey(h))
            .ToList();

        if (missing.Count > 0)
        {
            throw ErrorHelper.BadRequest(
                $"CSV is missing required columns: {string.Join(", ", missing)}.");
        }
    }

    private static List<OptionPair> BuildOptionPairs(Dictionary<string, int> headerMap)
    {
        var optionIndexes = new Dictionary<int, int>();

        foreach (var (header, index) in headerMap)
        {
            var match = OptionHeaderRegex.Match(header);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                optionIndexes[n] = index;
        }

        var pairs = new List<OptionPair>();

        foreach (var (n, optionIndex) in optionIndexes.OrderBy(x => x.Key))
        {
            var isCorrectHeader = $"IsCorrect{n}";
            if (!headerMap.TryGetValue(isCorrectHeader, out var isCorrectIndex))
            {
                throw ErrorHelper.BadRequest(
                    $"CSV header has Option{n} but is missing matching IsCorrect{n}.");
            }

            pairs.Add(new OptionPair(optionIndex, isCorrectIndex));
        }

        return pairs;
    }

    private static bool IsEmptyRow(
        CsvReader csv,
        Dictionary<string, int> headerMap,
        List<OptionPair> optionPairs)
    {
        if (!string.IsNullOrWhiteSpace(GetField(csv, headerMap, "QuestionText")))
            return false;

        if (!string.IsNullOrWhiteSpace(GetField(csv, headerMap, "QuestionType")))
            return false;

        if (!string.IsNullOrWhiteSpace(GetField(csv, headerMap, "Difficulty")))
            return false;

        if (!string.IsNullOrWhiteSpace(GetField(csv, headerMap, "Points")))
            return false;

        return optionPairs.All(pair => string.IsNullOrWhiteSpace(GetFieldByIndex(csv, pair.OptionIndex)));
    }

    private static string GetField(CsvReader csv, Dictionary<string, int> headerMap, string headerName)
    {
        if (!headerMap.TryGetValue(headerName, out var index))
            return string.Empty;

        return GetFieldByIndex(csv, index);
    }

    private static string GetFieldByIndex(CsvReader csv, int index)
    {
        try
        {
            return csv.GetField(index) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static decimal ParsePoints(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var points))
            return points;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out points))
            return points;

        return 0;
    }

    private static bool TryParseIsCorrect(string? raw, out bool isCorrect)
    {
        isCorrect = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        if (bool.TryParse(value, out isCorrect))
            return true;

        if (value.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            isCorrect = true;
            return true;
        }

        if (value.Equals("0", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private sealed record OptionPair(int OptionIndex, int IsCorrectIndex);
}
