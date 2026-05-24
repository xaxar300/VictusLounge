using VictusLounge.Data;
using VictusLounge.Models;
using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace VictusLounge.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public UnitOfWork()
        : this(new AppDbContext())
    {
    }

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        Users = new UserRepository(_dbContext);
        Computers = new ComputerRepository(_dbContext);
        Tariffs = new TariffRepository(_dbContext);
        Bookings = new BookingRepository(_dbContext);
        GameSessions = new GameSessionRepository(_dbContext);
        Payments = new PaymentRepository(_dbContext);
        Shifts = new ShiftRepository(_dbContext);
        PromoCodes = new PromoCodeRepository(_dbContext);
    }

    public IUserRepository Users { get; }
    public IComputerRepository Computers { get; }
    public ITariffRepository Tariffs { get; }
    public IBookingRepository Bookings { get; }
    public IGameSessionRepository GameSessions { get; }
    public IPaymentRepository Payments { get; }
    public IShiftRepository Shifts { get; }
    public IPromoCodeRepository PromoCodes { get; }

    public void BeginTransaction()
    {
        _transaction ??= _dbContext.Database.BeginTransaction();
    }

    public void CommitTransaction()
    {
        SaveChanges();
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void RollbackTransaction()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _dbContext.Dispose();
    }
}
