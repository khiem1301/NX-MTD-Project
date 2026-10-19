using Microsoft.EntityFrameworkCore;

namespace NX_MTD.Infrastructure.Data;

// File này KHÔNG bị scaffold --force ghi đè. Mọi cấu hình thêm chỉ đặt ở đây.
public partial class NxMtdDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // DB có trigger (trg_Employee_TeamLeadership, trg_StockTransaction_AppendOnly, ...):
        // EF phải dùng SELECT thay vì OUTPUT khi insert/update, nếu không SaveChanges sẽ lỗi.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => !e.IsKeyless && e.GetTableName() is not null)
                     .ToList())
        {
            modelBuilder.Entity(entityType.ClrType)
                .ToTable(entityType.GetTableName()!, entityType.GetSchema(), tb => tb.UseSqlOutputClause(false));
        }
    }
}
