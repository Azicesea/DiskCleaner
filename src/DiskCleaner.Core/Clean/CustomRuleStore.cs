using System.Text.Encodings.Web;
using System.Text.Json;

namespace DiskCleaner.Core.Clean;

/// <summary>
/// 用户自定义清理规则的存取。默认放在 %APPDATA%\DiskCleaner\custom-rules.json，
/// 文件坏了或读不出来时当作「没有自定义规则」，不影响程序启动。
/// </summary>
public static class CustomRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiskCleaner", "custom-rules.json");

    public static List<CleanRule> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<CleanRule>();
            var json = File.ReadAllText(FilePath);
            var dtos = JsonSerializer.Deserialize<List<RuleDto>>(json, JsonOptions);
            if (dtos is null) return new List<CleanRule>();

            var list = new List<CleanRule>();
            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.Name)) continue;
                var paths = (dto.Paths ?? Array.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();
                if (paths.Length == 0) continue;

                list.Add(new CleanRule
                {
                    Id = string.IsNullOrWhiteSpace(dto.Id) ? "custom-" + Guid.NewGuid().ToString("N") : dto.Id!,
                    Name = dto.Name!.Trim(),
                    Description = string.IsNullOrWhiteSpace(dto.Description)
                        ? "自定义规则"
                        : dto.Description!.Trim(),
                    Category = CleanCategory.Temp,
                    Kind = CleanRuleKind.Files,
                    Paths = paths,
                    UseRecycleBin = dto.UseRecycleBin,
                });
            }
            return list;
        }
        catch
        {
            return new List<CleanRule>();
        }
    }

    public static void Save(IEnumerable<CleanRule> rules)
    {
        var dtos = rules.Select(r => new RuleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Paths = r.Paths,
            UseRecycleBin = r.UseRecycleBin,
        }).ToList();

        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(dtos, JsonOptions));
    }

    private sealed class RuleDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string[]? Paths { get; set; }
        public bool UseRecycleBin { get; set; } = true;
    }
}
