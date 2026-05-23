using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace VictusLounge.Data;

public static class DbSetIdExtensions
{
    public static int GetNextId<TEntity>(this DbSet<TEntity> dbSet, Expression<Func<TEntity, int>> idSelector)
        where TEntity : class
    {
        return dbSet.Any() ? dbSet.Max(idSelector) + 1 : 1;
    }
}
