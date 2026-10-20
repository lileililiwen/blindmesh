using System.Text.Json;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Blindmesh.Infrastructure.Capabilities;

/// <summary>
/// The tables of the capabilities module. The migration stream of the
/// infrastructure owns them; no other module changes them.
/// </summary>
public static class CapabilityModelConfiguration
{
    /// <summary>
    /// The metadata of a capability is a small map of non sensitive
    /// identifiers. It is stored as a JSON object, so the database never holds
    /// a key that the allowlist of the domain refused.
    /// </summary>
    private static readonly ValueConverter<Dictionary<string, string>, string> MetadataConverter = new(
        metadata => JsonSerializer.Serialize(metadata),
        json => string.IsNullOrEmpty(json)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// The comparer of the metadata. The map is a collection, so the change
    /// detection of the context needs a comparer of its own.
    /// </summary>
    private static readonly ValueComparer<Dictionary<string, string>> MetadataComparer = new(
        (left, right) => left == null
            ? right == null
            : right != null &&
              left.Count == right.Count &&
              left.All(entry => right.ContainsKey(entry.Key) && right[entry.Key] == entry.Value),
        value => value.Aggregate(
            0,
            (hash, entry) => HashCode.Combine(hash, entry.Key.GetHashCode(), entry.Value.GetHashCode())),
        value => new Dictionary<string, string>(value, StringComparer.Ordinal));

    public static void ApplyCapabilityModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Capability>(entity =>
        {
            entity.ToTable("capabilities");
            entity.HasKey(capability => capability.Id);
            entity.Property(capability => capability.Id).HasColumnName("id");
            entity.Property(capability => capability.OwnerId).HasColumnName("owner_id").IsRequired();
            entity.Property(capability => capability.Kind)
                .HasColumnName("kind")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(capability => capability.Label)
                .HasColumnName("label")
                .HasMaxLength(CapabilityRules.LabelMaxLength)
                .IsRequired();
            entity.Property(capability => capability.Visibility)
                .HasColumnName("visibility")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(capability => capability.Available).HasColumnName("available").IsRequired();
            entity.Property(capability => capability.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(capability => capability.Verification)
                .HasColumnName("verification")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            var metadata = entity.Property(capability => capability.Metadata);

            metadata.HasColumnName("metadata");
            metadata.HasColumnType("jsonb");
            metadata.HasConversion(MetadataConverter);
            metadata.IsRequired();
            metadata.Metadata.SetValueComparer(MetadataComparer);
            entity.Property(capability => capability.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(capability => capability.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(capability => capability.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(capability => new { capability.CreatedAt, capability.Id })
                .HasDatabaseName("ix_capabilities_created_at_id");
            entity.HasIndex(capability => new { capability.OwnerId, capability.CreatedAt })
                .HasDatabaseName("ix_capabilities_owner_id_created_at");
            entity.HasIndex(capability => new { capability.Visibility, capability.Available, capability.Status })
                .HasDatabaseName("ix_capabilities_visibility_available_status");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(capability => capability.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CapabilityTag>(entity =>
        {
            entity.ToTable("capability_tags");
            entity.HasKey(tag => tag.Id);
            entity.Property(tag => tag.Id).HasColumnName("id");
            entity.Property(tag => tag.CapabilityId).HasColumnName("capability_id").IsRequired();
            entity.Property(tag => tag.Tag)
                .HasColumnName("tag")
                .HasMaxLength(CapabilityRules.TagMaxLength)
                .IsRequired();
            entity.HasIndex(tag => new { tag.CapabilityId, tag.Tag })
                .IsUnique()
                .HasDatabaseName("ux_capability_tags_capability_id_tag");
            entity.HasOne<Capability>()
                .WithMany()
                .HasForeignKey(tag => tag.CapabilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CapabilityStatusChange>(entity =>
        {
            entity.ToTable("capability_status_history");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id).HasColumnName("id");
            entity.Property(change => change.CapabilityId).HasColumnName("capability_id").IsRequired();
            entity.Property(change => change.FromStatus)
                .HasColumnName("from_status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(change => change.ToStatus)
                .HasColumnName("to_status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(change => change.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(change => change.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(change => new { change.CapabilityId, change.ChangedAt })
                .HasDatabaseName("ix_capability_status_history_capability_id_changed_at");
            entity.HasOne<Capability>()
                .WithMany()
                .HasForeignKey(change => change.CapabilityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(change => change.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
