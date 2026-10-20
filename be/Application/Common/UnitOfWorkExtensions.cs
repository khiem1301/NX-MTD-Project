using NX_MTD.Application.Interfaces;

namespace NX_MTD.Application.Common;

public static class UnitOfWorkExtensions
{
    /// <summary>Chạy nghiệp vụ nhiều bước trong một transaction: thành công thì commit, Failure hoặc exception thì rollback.</summary>
    public static async Task<TResult> InTransactionAsync<TResult>(this IUnitOfWork uow, Func<Task<TResult>> work)
        where TResult : Result
    {
        await uow.BeginTransactionAsync();
        try
        {
            var result = await work();
            if (result.IsSuccess) await uow.CommitTransactionAsync();
            else await uow.RollbackTransactionAsync();
            return result;
        }
        catch
        {
            await uow.RollbackTransactionAsync();
            throw;
        }
    }
}
