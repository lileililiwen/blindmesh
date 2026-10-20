using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Projects;

/// <summary>
/// The tables of the projects module. The migration stream of the
/// infrastructure owns them; no other module changes them.
/// </summary>
public static class ProjectModelConfiguration
{
    public static void ApplyProjectModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Id).HasColumnName("id");
            entity.Property(project => project.OwnerId).HasColumnName("owner_id").IsRequired();
            entity.Property(project => project.Name)
                .HasColumnName("name")
                .HasMaxLength(ProjectRules.NameMaxLength)
                .IsRequired();
            entity.Property(project => project.Summary)
                .HasColumnName("summary")
                .HasMaxLength(ProjectRules.SummaryMaxLength)
                .IsRequired();
            entity.Property(project => project.Description)
                .HasColumnName("description")
                .HasMaxLength(ProjectRules.DescriptionMaxLength)
                .IsRequired();
            entity.Property(project => project.RepositoryUrl)
                .HasColumnName("repository_url")
                .HasMaxLength(ProjectRules.RepositoryUrlMaxLength)
                .IsRequired();
            entity.Property(project => project.Visibility)
                .HasColumnName("visibility")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(project => project.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(project => project.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(project => project.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(project => project.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(project => new { project.CreatedAt, project.Id })
                .HasDatabaseName("ix_projects_created_at_id");
            entity.HasIndex(project => project.OwnerId).HasDatabaseName("ix_projects_owner_id");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("project_members");
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Id).HasColumnName("id");
            entity.Property(member => member.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(member => member.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(member => member.Role)
                .HasColumnName("role")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(member => member.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(member => new { member.ProjectId, member.UserId })
                .IsUnique()
                .HasDatabaseName("ux_project_members_project_id_user_id");
            entity.HasIndex(member => member.UserId).HasDatabaseName("ix_project_members_user_id");
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(member => member.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectVersion>(entity =>
        {
            entity.ToTable("project_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.Id).HasColumnName("id");
            entity.Property(version => version.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(version => version.Label)
                .HasColumnName("label")
                .HasMaxLength(ProjectRules.VersionLabelMaxLength)
                .IsRequired();
            entity.Property(version => version.CommitReference)
                .HasColumnName("commit_reference")
                .HasMaxLength(ProjectRules.CommitReferenceMaxLength);
            entity.Property(version => version.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(version => version.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(version => new { version.ProjectId, version.CreatedAt })
                .HasDatabaseName("ix_project_versions_project_id_created_at");
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(version => version.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(version => version.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectStatusChange>(entity =>
        {
            entity.ToTable("project_status_history");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id).HasColumnName("id");
            entity.Property(change => change.ProjectId).HasColumnName("project_id").IsRequired();
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
            entity.HasIndex(change => new { change.ProjectId, change.ChangedAt })
                .HasDatabaseName("ix_project_status_history_project_id_changed_at");
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(change => change.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(change => change.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
