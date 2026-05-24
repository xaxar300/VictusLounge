using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace VictusLounge.Repositories;

public interface IRepository<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> Query();
    IQueryable<TEntity> QueryNoTracking();
    List<TEntity> GetAll();
    TEntity? GetById(int id);
    TEntity? FirstOrDefault(Expression<Func<TEntity, bool>> predicate);
    bool Any();
    bool Any(Expression<Func<TEntity, bool>> predicate);
    int Count(Expression<Func<TEntity, bool>> predicate);
    int GetNextId(Expression<Func<TEntity, int>> idSelector);
    Task<int> GetNextIdAsync(Expression<Func<TEntity, int>> idSelector);
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
}
