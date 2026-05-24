using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;

namespace VictusLounge.Repositories;

public class Repository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet;

    public Repository(AppDbContext dbContext)
    {
        _dbSet = dbContext.Set<TEntity>();
    }

    public IQueryable<TEntity> Query()
    {
        return _dbSet;
    }

    public IQueryable<TEntity> QueryNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    public List<TEntity> GetAll()
    {
        return _dbSet.ToList();
    }

    public TEntity? GetById(int id)
    {
        return _dbSet.Find(id);
    }

    public TEntity? FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
    {
        return _dbSet.FirstOrDefault(predicate);
    }

    public bool Any()
    {
        return _dbSet.Any();
    }

    public bool Any(Expression<Func<TEntity, bool>> predicate)
    {
        return _dbSet.Any(predicate);
    }

    public int Count(Expression<Func<TEntity, bool>> predicate)
    {
        return _dbSet.Count(predicate);
    }

    public int GetNextId(Expression<Func<TEntity, int>> idSelector)
    {
        return _dbSet.Any() ? _dbSet.Max(idSelector) + 1 : 1;
    }

    public async Task<int> GetNextIdAsync(Expression<Func<TEntity, int>> idSelector)
    {
        return await _dbSet.AnyAsync()
            ? await _dbSet.MaxAsync(idSelector) + 1
            : 1;
    }

    public void Add(TEntity entity)
    {
        _dbSet.Add(entity);
    }

    public void AddRange(IEnumerable<TEntity> entities)
    {
        _dbSet.AddRange(entities);
    }

    public void Remove(TEntity entity)
    {
        _dbSet.Remove(entity);
    }
}
