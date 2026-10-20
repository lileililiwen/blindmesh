using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Tasks;

/// <summary>
/// The tables of the tasks module. They are owned by the tasks package and are
/// not changed by any other module.
/// </summary>
public static class TaskModelConfiguration
{
    public static void ApplyTaskModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketplaceTask>(entity =>
        {
            entity.ToTable("tasks");
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Id).HasColumnName("id");
            entity.Property(task => task.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(task => task.OwnerId).HasColumnName("owner_id").IsRequired();
            entity.Property(task => task.Title).HasColumnName("title").HasMaxLength(140).IsRequired();
            entity.Property(task => task.Scope).HasColumnName("scope").HasMaxLength(10000).IsRequired();
            entity.Property(task => task.Modes).HasColumnName("modes").IsRequired();
            entity.Property(task => task.Visibility)
                .HasColumnName("visibility")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(task => task.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(task => task.ExpectedMinutes).HasColumnName("expected_minutes").IsRequired();
            entity.Property(task => task.BaseReward).HasColumnName("base_reward").IsRequired();
            entity.Property(task => task.SlotBonus).HasColumnName("slot_bonus").IsRequired();
            entity.Property(task => task.SlotCount).HasColumnName("slot_count").IsRequired();
            entity.Property(task => task.Deadline).HasColumnName("deadline").HasColumnType("timestamptz");
            entity.Property(task => task.AcceptsCleanFindings).HasColumnName("accepts_clean_findings").IsRequired();
            entity.Property(task => task.AccessKind)
                .HasColumnName("access_kind")
                .HasMaxLength(32)
                .HasConversion<string>();
            entity.Property(task => task.AccessResource).HasColumnName("access_resource").HasMaxLength(500);
            entity.Property(task => task.ReviewCriteria).HasColumnName("review_criteria").HasMaxLength(2000);
            entity.Property(task => task.ReservedBudget).HasColumnName("reserved_budget").IsRequired();
            entity.Property(task => task.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(task => task.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(task => task.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(task => task.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz");
            entity.Property(task => task.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamptz");
            entity.HasIndex(task => new { task.CreatedAt, task.Id })
                .HasDatabaseName("ix_tasks_created_at_id");
            entity.HasIndex(task => new { task.ProjectId, task.CreatedAt })
                .HasDatabaseName("ix_tasks_project_id_created_at");
            entity.HasIndex(task => new { task.Status, task.Modes })
                .HasDatabaseName("ix_tasks_status_modes");
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Blindmesh.Domain.Identity.User>()
                .WithMany()
                .HasForeignKey(task => task.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskRequirement>(entity =>
        {
            entity.ToTable("task_requirements");
            entity.HasKey(requirement => requirement.Id);
            entity.Property(requirement => requirement.Id).HasColumnName("id");
            entity.Property(requirement => requirement.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(requirement => requirement.Kind).HasColumnName("kind").HasMaxLength(16).IsRequired();
            entity.Property(requirement => requirement.Value).HasColumnName("value").HasMaxLength(100).IsRequired();
            entity.HasIndex(requirement => requirement.TaskId)
                .HasDatabaseName("ix_task_requirements_task_id");
            entity.HasIndex(requirement => new { requirement.Kind, requirement.Value, requirement.TaskId })
                .HasDatabaseName("ix_task_requirements_kind_value_task_id");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(requirement => requirement.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskStep>(entity =>
        {
            entity.ToTable("task_steps");
            entity.HasKey(step => step.Id);
            entity.Property(step => step.Id).HasColumnName("id");
            entity.Property(step => step.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(step => step.Position).HasColumnName("position").IsRequired();
            entity.Property(step => step.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(step => step.ExpectedResult).HasColumnName("expected_result").HasMaxLength(2000).IsRequired();
            entity.HasIndex(step => new { step.TaskId, step.Position })
                .HasDatabaseName("ix_task_steps_task_id_position");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(step => step.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskSlot>(entity =>
        {
            entity.ToTable("task_slots");
            entity.HasKey(slot => slot.Id);
            entity.Property(slot => slot.Id).HasColumnName("id");
            entity.Property(slot => slot.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(slot => slot.Position).HasColumnName("position").IsRequired();
            entity.Property(slot => slot.Reward).HasColumnName("reward").IsRequired();
            entity.Property(slot => slot.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(slot => slot.ContributorId).HasColumnName("contributor_id");
            entity.Property(slot => slot.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");
            entity.HasIndex(slot => new { slot.TaskId, slot.Position })
                .HasDatabaseName("ix_task_slots_task_id_position");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(slot => slot.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskStatusChange>(entity =>
        {
            entity.ToTable("task_history");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id).HasColumnName("id");
            entity.Property(change => change.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(change => change.FromStatus).HasColumnName("from_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(change => change.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(change => new { change.TaskId, change.ChangedAt })
                .HasDatabaseName("ix_task_history_task_id_changed_at");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(change => change.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
