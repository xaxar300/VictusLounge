using System;
using System.Threading.Tasks;
using VictusLounge.Models;

namespace VictusLounge.Repositories;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IComputerRepository Computers { get; }
    ITariffRepository Tariffs { get; }
    IBookingRepository Bookings { get; }
    IGameSessionRepository GameSessions { get; }
    IPaymentRepository Payments { get; }
    IShiftRepository Shifts { get; }
    IPromoCodeRepository PromoCodes { get; }
    void BeginTransaction();
    void CommitTransaction();
    void RollbackTransaction();
    int SaveChanges();
    Task<int> SaveChangesAsync();
}
