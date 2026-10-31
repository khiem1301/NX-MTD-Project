using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Role", tb => tb.HasComment("Vai trò (9 vai trò, gồm Tổ trưởng và Tổ phó)"));

        entity.HasIndex(e => e.RoleCode, "UQ_Role_Code").IsUnique();

        entity.Property(e => e.RoleCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.RoleName).HasMaxLength(100);
    }
}
