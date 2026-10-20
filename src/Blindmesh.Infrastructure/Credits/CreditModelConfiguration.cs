using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Credits;

/// <summary>
/// The tables of the credits module. The migration stream of the
/// infrastructure owns them; no other module changes them. The journal is
/// append only, so the foreign keys of the entries never delete a row.
/// </summary>
public static class CreditModelConfiguration
{
    public static void ApplyCreditModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerAccount>(entity =>
        {
            entity.ToTable("ledger_accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.Bucket)
                .HasColumnName("bucket")
                .HasMaxLength(32)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(account => account.OwnerId).HasColumnName("owner_id");
            entity.Property(account => account.Reference)
                .HasColumnName("reference")
                .HasMaxLength(CreditRules.ReferenceMaxLength)
                .IsRequired();
            entity.Property(account => account.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(account => new { account.Bucket, account.OwnerId, account.Reference })
                .IsUnique()
                .HasDatabaseName("ux_ledger_accounts_bucket_owner_id_reference");
            entity.HasIndex(account => new { account.Bucket, account.Reference })
                .IsUnique()
                .HasFilter("owner_id IS NULL")
                .HasDatabaseName("ux_ledger_accounts_platform_bucket_reference");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(account => account.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerTransaction>(entity =>
        {
            entity.ToTable("ledger_transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Id).HasColumnName("id");
            entity.Property(transaction => transaction.Kind)
                .HasColumnName("kind")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(transaction => transaction.Reference)
                .HasColumnName("reference")
                .HasMaxLength(CreditRules.ReferenceMaxLength)
                .IsRequired();
            entity.Property(transaction => transaction.ActorId).HasColumnName("actor_id");
            entity.Property(transaction => transaction.IdempotencyKey)
                .HasColumnName("idempotency_key")
                .HasMaxLength(128);
            entity.Property(transaction => transaction.Note)
                .HasColumnName("note")
                .HasMaxLength(CreditRules.NoteMaxLength);
            entity.Property(transaction => transaction.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(transaction => new { transaction.CreatedAt, transaction.Id })
                .HasDatabaseName("ix_ledger_transactions_created_at_id");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(transaction => transaction.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("ledger_entries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.TransactionId).HasColumnName("transaction_id").IsRequired();
            entity.Property(entry => entry.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(entry => entry.Amount).HasColumnName("amount").IsRequired();
            entity.Property(entry => entry.LotId).HasColumnName("lot_id");
            entity.Property(entry => entry.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(entry => new { entry.AccountId, entry.CreatedAt })
                .HasDatabaseName("ix_ledger_entries_account_id_created_at");
            entity.HasIndex(entry => entry.TransactionId)
                .HasDatabaseName("ix_ledger_entries_transaction_id");
            entity.HasOne<LedgerTransaction>()
                .WithMany()
                .HasForeignKey(entry => entry.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerAccount>()
                .WithMany()
                .HasForeignKey(entry => entry.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CreditLot>()
                .WithMany()
                .HasForeignKey(entry => entry.LotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditLot>(entity =>
        {
            entity.ToTable("credit_lots");
            entity.HasKey(lot => lot.Id);
            entity.Property(lot => lot.Id).HasColumnName("id");
            entity.Property(lot => lot.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(lot => lot.Kind)
                .HasColumnName("kind")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(lot => lot.Source)
                .HasColumnName("source")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(lot => lot.Reference)
                .HasColumnName("reference")
                .HasMaxLength(CreditRules.ReferenceMaxLength)
                .IsRequired();
            entity.Property(lot => lot.OriginalAmount).HasColumnName("original_amount").IsRequired();
            entity.Property(lot => lot.AvailableAmount).HasColumnName("available_amount").IsRequired();
            entity.Property(lot => lot.ReservedAmount).HasColumnName("reserved_amount").IsRequired();
            entity.Property(lot => lot.PendingAmount).HasColumnName("pending_amount").IsRequired();
            entity.Property(lot => lot.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");
            entity.Property(lot => lot.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(lot => new { lot.UserId, lot.CreatedAt })
                .HasDatabaseName("ix_credit_lots_user_id_created_at");
            entity.HasIndex(lot => new { lot.UserId, lot.Reference })
                .IsUnique()
                .HasFilter("source = 'pending'")
                .HasDatabaseName("ux_credit_lots_pending_user_id_reference");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(lot => lot.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrantClaim>(entity =>
        {
            entity.ToTable("grant_claims");
            entity.HasKey(claim => claim.Id);
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.Provider)
                .HasColumnName("provider")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.Subject)
                .HasColumnName("subject")
                .HasMaxLength(128)
                .IsRequired();
            entity.Property(claim => claim.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(claim => claim.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(claim => claim.TransactionId).HasColumnName("transaction_id").IsRequired();
            entity.Property(claim => claim.Amount).HasColumnName("amount").IsRequired();
            entity.Property(claim => claim.IpAddress)
                .HasColumnName("ip_address")
                .HasMaxLength(64);
            entity.Property(claim => claim.RulesAcceptedAt).HasColumnName("rules_accepted_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(claim => claim.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(claim => claim.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(claim => new { claim.Provider, claim.Subject })
                .IsUnique()
                .HasDatabaseName("ux_grant_claims_provider_subject");
            entity.HasIndex(claim => claim.IssuedAt)
                .HasDatabaseName("ix_grant_claims_issued_at");
            entity.HasIndex(claim => new { claim.IpAddress, claim.IssuedAt })
                .HasDatabaseName("ix_grant_claims_ip_address_issued_at");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(claim => claim.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerAccount>()
                .WithMany()
                .HasForeignKey(claim => claim.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerTransaction>()
                .WithMany()
                .HasForeignKey(claim => claim.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
