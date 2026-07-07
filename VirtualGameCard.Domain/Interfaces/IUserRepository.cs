using System.Linq.Expressions;
using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task<TResult?> GetByIdAsync<TResult>(Guid id, Expression<Func<User, TResult>> selector);
    Task<bool> ExistsByEmailAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
