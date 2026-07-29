using backend.Models.Plc;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class PlcPresetSeeder
{
    public static async Task SeedAsync(PlantMonitorDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var presets = new List<PlcProtocolPresetEntity>
        {
            new()
            {
                Manufacturer = "AllenBradley",
                PresetName = "BasicStatus",
                PresetVersion = 1,
                Description = "Allen-Bradley baseline telemetry preset.",
                Tags =
                [
                    new PlcProtocolPresetTagEntity { TagKey = "status", PlcAddress = "Program:LineData.Status", DataType = "int", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "product", PlcAddress = "Program:LineData.ProductSerial", DataType = "string", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "runtime_seconds", PlcAddress = "Program:LineData.RuntimeSeconds", DataType = "dint", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "total_length", PlcAddress = "Program:LineData.TotalLength", DataType = "real", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "control_mode", PlcAddress = "Program:LineData.ControlMode", DataType = "int", Scale = 1.0m, IsRequired = true },
                ],
            },
            new()
            {
                Manufacturer = "Siemens",
                PresetName = "BasicStatus",
                PresetVersion = 1,
                Description = "Siemens baseline telemetry preset.",
                Tags =
                [
                    new PlcProtocolPresetTagEntity { TagKey = "status", PlcAddress = "DB12.DBW0", DataType = "int", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "product", PlcAddress = "DB12.DBD4", DataType = "string", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "runtime_seconds", PlcAddress = "DB12.DBD12", DataType = "dint", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "total_length", PlcAddress = "DB12.DBD20", DataType = "real", Scale = 1.0m, IsRequired = true },
                    new PlcProtocolPresetTagEntity { TagKey = "control_mode", PlcAddress = "DB12.DBW24", DataType = "int", Scale = 1.0m, IsRequired = true },
                ],
            },
        };

        var existing = await dbContext.PlcProtocolPresets
            .AsNoTracking()
            .Select(x => new { x.Manufacturer, x.PresetName, x.PresetVersion })
            .ToListAsync(cancellationToken);

        var existingKeys = new HashSet<string>(
            existing.Select(x => BuildKey(x.Manufacturer, x.PresetName, x.PresetVersion)),
            StringComparer.OrdinalIgnoreCase);

        var missing = presets
            .Where(x => !existingKeys.Contains(BuildKey(x.Manufacturer, x.PresetName, x.PresetVersion)))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.PlcProtocolPresets.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildKey(string manufacturer, string presetName, int presetVersion)
    {
        return $"{manufacturer}::{presetName}::{presetVersion}";
    }
}
