using System.Globalization;
using System.Text;
using System.Text.Json;
using FinanceOS.Core;

namespace FinanceOS.Infrastructure;

public sealed class JsonTaskExchange : ITaskExchangeAdapter
{
    public string Format => "JSON";
    public string Export(IReadOnlyList<WorkTask> tasks) => JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
    public IReadOnlyList<WorkTask> Import(string content) => JsonSerializer.Deserialize<List<WorkTask>>(content) ?? throw new ArgumentException("Empty task document.");
}

public sealed class CsvTaskExchange : ITaskExchangeAdapter
{
    public string Format => "CSV";
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r\n'".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public string Export(IReadOnlyList<WorkTask> tasks)
    {
        var b = new StringBuilder("Id,Title,Notes,DueDate,Priority,Status,CreatedUtc,UpdatedUtc\r\n");
        foreach (var t in tasks)
            b.AppendLine(string.Join(",", new[] { t.Id, t.Title, t.Notes, t.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", t.Priority, t.Status, t.CreatedUtc.ToString("O"), t.UpdatedUtc.ToString("O") }.Select(Cell)));
        return b.ToString();
    }
    public IReadOnlyList<WorkTask> Import(string content)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '"') { if (quoted && i + 1 < content.Length && content[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
            else if (!quoted && ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (!quoted && ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new();
            }
            else cell.Append(ch);
        }
        if (quoted) throw new ArgumentException("Unclosed CSV quote.");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        if (rows.Count == 0 || string.Join(",", rows[0]) != "Id,Title,Notes,DueDate,Priority,Status,CreatedUtc,UpdatedUtc") throw new ArgumentException("Use the FINANCE OS task CSV columns.");
        foreach (var fields in rows.Skip(1))
            for (var i = 0; i < fields.Count; i++)
                if (fields[i].Length > 1 && fields[i][0] == '\'' && "=+-@\t\r\n'".Contains(fields[i][1])) fields[i] = fields[i][1..];
        return rows.Skip(1).Select(r => r.Count != 8 ? throw new ArgumentException("Invalid CSV row.") : new WorkTask(r[0], r[1], r[2],
            r[3].Length == 0 ? null : DateOnly.ParseExact(r[3], "yyyy-MM-dd", CultureInfo.InvariantCulture), r[4], r[5], null, null,
            DateTimeOffset.Parse(r[6], CultureInfo.InvariantCulture), DateTimeOffset.Parse(r[7], CultureInfo.InvariantCulture))).ToList();
    }
}
