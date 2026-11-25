using System.Text.Json;

try
{
    if (args.Length != 1) throw new ArgumentException("Pass one JSON input file");
    using var data = JsonDocument.Parse(File.ReadAllText(args[0]));
    Console.WriteLine(JsonSerializer.Serialize(Analyze(data.RootElement)));
    return 0;
}
catch (Exception error) when (error is IOException or JsonException or FormatException
                               or ArgumentException or InvalidOperationException
                               or KeyNotFoundException or ArithmeticException or IndexOutOfRangeException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

static object Analyze(JsonElement input)
{
    decimal roof = input.GetProperty("roof_m2").GetDecimal();
    decimal efficiency = input.GetProperty("efficiency").GetDecimal();
    decimal capacity = input.GetProperty("capacity_l").GetDecimal();
    decimal initial = input.GetProperty("initial_l").GetDecimal();
    if (roof <= 0 || capacity <= 0 || efficiency < 0 || efficiency > 1 || initial < 0 || initial > capacity)
        throw new ArgumentException("Use positive roof area and capacity, efficiency from 0 to 1, and initial storage within capacity.");

    var entries = new List<(DateOnly Date, decimal Rain, decimal Use)>();
    var seenDates = new HashSet<DateOnly>();
    foreach (var row in input.GetProperty("days").EnumerateArray())
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (!DateOnly.TryParseExact(row.GetProperty("date").GetString(), "yyyy-MM-dd", culture,
            System.Globalization.DateTimeStyles.None, out var date) || !seenDates.Add(date))
            throw new ArgumentException("Each modeled day needs a unique date in YYYY-MM-DD format.");
        decimal rain = row.GetProperty("rain_mm").GetDecimal();
        decimal use = row.GetProperty("use_l").GetDecimal();
        if (rain < 0 || use < 0) throw new ArgumentException("Rainfall and use must be nonnegative.");
        entries.Add((date, rain, use));
    }
    if (entries.Count == 0) throw new ArgumentException("Supply at least one dated rainfall and use row.");

    decimal remaining = initial;
    decimal capturedTotal = 0, overflowTotal = 0, deliveredTotal = 0, unmetTotal = 0;
    var daily = new List<object>();
    foreach (var entry in entries.OrderBy(entry => entry.Date))
    {
        decimal captured = roof * entry.Rain * efficiency;
        decimal freeSpace = capacity - remaining;
        decimal stored = Math.Min(freeSpace, captured);
        decimal overflow = captured - stored;
        remaining += stored;
        decimal delivered = Math.Min(remaining, entry.Use);
        decimal unmet = entry.Use - delivered;
        remaining -= delivered;
        capturedTotal += captured;
        overflowTotal += overflow;
        deliveredTotal += delivered;
        unmetTotal += unmet;
        daily.Add(new
        {
            date = entry.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            captured_l = captured, overflow_l = overflow, delivered_l = delivered,
            unmet_l = unmet, ending_l = remaining,
        });
    }
    return new
    {
        initial_l = initial, captured_l = capturedTotal, overflow_l = overflowTotal,
        delivered_l = deliveredTotal, unmet_l = unmetTotal, ending_l = remaining, days = daily,
    };
}
