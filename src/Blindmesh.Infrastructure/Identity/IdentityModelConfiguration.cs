using Blindmesh.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// The tables of the identity module. The migration stream of the
/// infrastructure owns them; no other module changes them.
/// </summary>
public static class IdentityModelConfiguration
{
    public static void ApplyIdentityModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.GithubLogin).HasColumnName("github_login").HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.Username).HasColumnName("username").HasMaxLength(30).IsRequired();
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(80).IsRequired();
            entity.Property(user => user.Bio).HasColumnName("bio").HasMaxLength(1000).IsRequired();
            entity.Property(user => user.Timezone).HasColumnName("timezone").HasMaxLength(64).IsRequired();
            entity.Property(user => user.Skills).HasColumnName("skills").HasColumnType("text[]").IsRequired();
            entity.Property(user => user.Languages).HasColumnName("languages").HasColumnType("text[]").IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
            entity.Property(user => user.IsBanned).HasColumnName("is_banned").IsRequired();
            entity.Property(user => user.IsPublic).HasColumnName("is_public").IsRequired();
            entity.Property(user => user.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(user => user.Username)
                .IsUnique()
                .HasDatabaseName("ux_users_username");
            entity.HasIndex(user => user.GithubLogin)
                .HasDatabaseName("ix_users_github_login");
        });

        modelBuilder.Entity<ExternalIdentity>(entity =>
        {
            entity.ToTable("external_identities");
            entity.HasKey(identity => identity.Id);
            entity.Property(identity => identity.Id).HasColumnName("id");
            entity.Property(identity => identity.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(identity => identity.Provider).HasColumnName("provider").HasMaxLength(32).IsRequired();
            entity.Property(identity => identity.Subject).HasColumnName("subject").HasMaxLength(128).IsRequired();
            entity.Property(identity => identity.AccountCreatedAt).HasColumnName("account_created_at").HasColumnType("timestamptz");
            entity.Property(identity => identity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(identity => new { identity.Provider, identity.Subject })
                .IsUnique()
                .HasDatabaseName("ux_external_identities_provider_subject");
            entity.HasIndex(identity => identity.UserId)
                .HasDatabaseName("ix_external_identities_user_id");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(identity => identity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Id).HasColumnName("id");
            entity.Property(session => session.UserId).HasColumnName("user_id");
            entity.Property(session => session.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(session => session.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(session => session.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(session => session.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(session => session.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz");
            entity.HasIndex(session => session.TokenHash)
                .IsUnique()
                .HasDatabaseName("ux_sessions_token_hash");
            entity.HasIndex(session => session.UserId)
                .HasDatabaseName("ix_sessions_user_id");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OAuthState>(entity =>
        {
            entity.ToTable("oauth_states");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.Id).HasColumnName("id");
            entity.Property(state => state.StateHash).HasColumnName("state_hash").HasMaxLength(64).IsRequired();
            entity.Property(state => state.BindingHash).HasColumnName("binding_hash").HasMaxLength(64).IsRequired();
            entity.Property(state => state.ReturnPath).HasColumnName("return_path").HasMaxLength(200).IsRequired();
            entity.Property(state => state.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(state => state.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(state => state.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamptz");
            entity.HasIndex(state => state.StateHash)
                .IsUnique()
                .HasDatabaseName("ux_oauth_states_state_hash");
        });
    }
}
