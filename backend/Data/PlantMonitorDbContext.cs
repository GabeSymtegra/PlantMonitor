using backend.Models.Plc;
using backend.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public sealed class PlantMonitorDbContext : DbContext
{
    public PlantMonitorDbContext(DbContextOptions<PlantMonitorDbContext> options)
        : base(options)
    {
    }

    public DbSet<PlcProtocolPresetEntity> PlcProtocolPresets => Set<PlcProtocolPresetEntity>();
    public DbSet<PlcProtocolPresetTagEntity> PlcProtocolPresetTags => Set<PlcProtocolPresetTagEntity>();
    public DbSet<LineProtocolAssignmentEntity> LineProtocolAssignments => Set<LineProtocolAssignmentEntity>();
    public DbSet<LineTagOverrideEntity> LineTagOverrides => Set<LineTagOverrideEntity>();
    public DbSet<LineTagCatalogEntryEntity> LineTagCatalogEntries => Set<LineTagCatalogEntryEntity>();
    public DbSet<RecipeToleranceEntity> RecipeTolerances => Set<RecipeToleranceEntity>();
    public DbSet<CompletedProductionRunEntity> CompletedProductionRuns => Set<CompletedProductionRunEntity>();
    public DbSet<CompletedProductionRunZoneStatEntity> CompletedProductionRunZoneStats => Set<CompletedProductionRunZoneStatEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlcProtocolPresetEntity>(entity =>
        {
            entity.ToTable("plc_protocol_presets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Manufacturer).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PresetName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => new { x.Manufacturer, x.PresetName, x.PresetVersion }).IsUnique();
            entity.HasMany(x => x.Tags)
                .WithOne(x => x.Preset)
                .HasForeignKey(x => x.PresetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlcProtocolPresetTagEntity>(entity =>
        {
            entity.ToTable("plc_protocol_preset_tags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TagKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlcAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DataType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Scale).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.PresetId, x.TagKey }).IsUnique();
        });

        modelBuilder.Entity<LineProtocolAssignmentEntity>(entity =>
        {
            entity.ToTable("line_protocol_assignments");
            entity.HasKey(x => x.LineId);
            entity.Property(x => x.LineNumber).IsRequired();
            entity.Property(x => x.LineName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ProductId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlcIp).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Manufacturer).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PresetName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.HasMany(x => x.TagOverrides)
                .WithOne(x => x.Assignment)
                .HasForeignKey(x => x.LineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LineTagOverrideEntity>(entity =>
        {
            entity.ToTable("line_tag_overrides");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TagKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlcAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DataType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Scale).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.LineId, x.TagKey }).IsUnique();
        });

        modelBuilder.Entity<LineTagCatalogEntryEntity>(entity =>
        {
            entity.ToTable("line_tag_catalog_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LogicalKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Driver).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PlcAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DataType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(32);
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.Scale).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.LineId, x.LogicalKey }).IsUnique();
            entity.HasIndex(x => new { x.LineId, x.PlcAddress }).IsUnique();
        });

        modelBuilder.Entity<RecipeToleranceEntity>(entity =>
        {
            entity.ToTable("recipe_tolerances");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecipeId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ProductId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.MeasurementType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TargetValue).HasPrecision(18, 6);
            entity.Property(x => x.ToleranceMinus).HasPrecision(18, 6);
            entity.Property(x => x.TolerancePlus).HasPrecision(18, 6);
            entity.Property(x => x.Source).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.RecipeId, x.MeasurementType, x.Version }).IsUnique();
        });

        modelBuilder.Entity<CompletedProductionRunEntity>(entity =>
        {
            entity.ToTable("completed_production_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LineName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ProductId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RecipeId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.MachineId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.OperatorName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Manufacturer).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PlcIp).HasMaxLength(64).IsRequired();
            entity.Property(x => x.FinalStatus).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.LineId);
            entity.HasIndex(x => x.EndTimeUtc);
            entity.HasMany(x => x.ZoneStats)
                .WithOne(x => x.Run)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompletedProductionRunZoneStatEntity>(entity =>
        {
            entity.ToTable("completed_production_run_zone_stats");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Zone).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Segment).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.RunId, x.Zone, x.Segment }).IsUnique();
        });
    }
}
