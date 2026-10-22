using Blindmesh.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Notifications;

/// <summary>
/// The tables of the notification module. They are owned by the notification
/// package and are not changed by any other module.
/// </summary>
public static class NotificationModelConfiguration
{
    public static void ApplyNotificationModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries");
            entity.HasKey(delivery => delivery.Id);
            entity.Property(delivery => delivery.Id).HasColumnName("id");
            entity.Property(delivery => delivery.RecipientId).HasColumnName("recipient_id").IsRequired();
            entity.Property(delivery => delivery.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
            entity.Property(delivery => delivery.Category)
                .HasColumnName("category")
                .HasMaxLength(32)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(delivery => delivery.Channel)
                .HasColumnName("channel")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(delivery => delivery.UniqueKey).HasColumnName("unique_key").HasMaxLength(300).IsRequired();
            entity.Property(delivery => delivery.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
            entity.Property(delivery => delivery.Summary).HasColumnName("summary").HasMaxLength(400).IsRequired();
            entity.Property(delivery => delivery.DeepLink).HasColumnName("deep_link").HasMaxLength(200).IsRequired();
            entity.Property(delivery => delivery.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(delivery => delivery.AttemptCount).HasColumnName("attempt_count").IsRequired();
            entity.Property(delivery => delivery.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(delivery => delivery.MessageId).HasColumnName("message_id").HasMaxLength(200).IsRequired();
            entity.Property(delivery => delivery.LastError).HasColumnName("last_error").HasMaxLength(200);
            entity.Property(delivery => delivery.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(delivery => delivery.SentAt).HasColumnName("sent_at").HasColumnType("timestamptz");
            entity.Property(delivery => delivery.ReadAt).HasColumnName("read_at").HasColumnType("timestamptz");
            entity.Property(delivery => delivery.DeadLetteredAt).HasColumnName("dead_lettered_at").HasColumnType("timestamptz");

            // The identity of a delivery is the event, the recipient and the
            // channel. It is unique, so a redelivered event never writes a
            // second row for the same identity.
            entity.HasIndex(delivery => delivery.UniqueKey)
                .IsUnique()
                .HasDatabaseName("ux_notification_deliveries_unique_key");
            entity.HasIndex(delivery => new { delivery.RecipientId, delivery.CreatedAt })
                .HasDatabaseName("ix_notification_deliveries_recipient_created_at");
            entity.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt })
                .HasDatabaseName("ix_notification_deliveries_status_next_attempt_at");
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(preference => preference.UserId);
            entity.Property(preference => preference.UserId).HasColumnName("user_id");
            entity.Property(preference => preference.InAppEnabled).HasColumnName("in_app_enabled").IsRequired();
            entity.Property(preference => preference.EmailEnabled).HasColumnName("email_enabled").IsRequired();
            entity.Property(preference => preference.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
        });
    }
}
