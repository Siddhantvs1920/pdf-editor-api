using Microsoft.EntityFrameworkCore;
using PdfEditorApi.Models;

namespace PdfEditorApi.Data;

public sealed class SOPDbContext(DbContextOptions<SOPDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SOPInstance> SOPInstances => Set<SOPInstance>();
    public DbSet<SOPAuditLog> SOPAuditLogs => Set<SOPAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("UserId");
            entity.Property(x => x.UserName).HasColumnName("Username").HasMaxLength(120).IsRequired();
            entity.Ignore(x => x.DisplayName);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedAt);
        });

        modelBuilder.Entity<SOPInstance>(entity =>
        {
            entity.ToTable("SOPInstances");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
            entity.Property(x => x.CurrentContentHtml).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.TemplateHtml).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.EditableSectionSelectorsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedByUserId).IsRequired();
            entity.Property(x => x.UploadedAtUtc).IsRequired();
            entity.Property(x => x.Area).HasMaxLength(200);
            entity.Property(x => x.Line).HasMaxLength(200);
            entity.Property(x => x.ExpiryDateUtc);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<SOPAuditLog>(entity =>
        {
            entity.ToTable("SOPAuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorRole).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Comments).HasMaxLength(2000);
            entity.Property(x => x.StatusAfterAction).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.TimestampUtc).IsRequired();
        });
    }
}
