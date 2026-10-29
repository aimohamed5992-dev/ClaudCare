using ClaudCare.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Infrastructure;

/// <summary>
/// يشغّل العمل داخل معاملة بعد قفل صف واحد (<c>SELECT … FOR UPDATE</c>) فلا يُنفَّذ قرار أو دفع أو صرف مرتين عند الطلبات المتزامنة.
/// اسم الجدول من ثوابت الكود فقط (لا يأتي من المستخدم).
/// </summary>
public static class RowLock
{
    public static async Task<T> RunAsync<T>(ClaudCareDbContext db, string table, int id, Func<Task<T>> work, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync($"SELECT Id FROM `{table}` WHERE Id = {id} FOR UPDATE", ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
