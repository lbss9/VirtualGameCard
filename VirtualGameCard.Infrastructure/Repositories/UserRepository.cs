using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower());

    public Task<User?> GetByIdAsync(Guid id) => db.Users.FirstOrDefaultAsync(user => user.Id == id);

    public async Task<TResult?> GetByIdAsync<TResult>(
        Guid id,
        Expression<Func<User, TResult>> selector
    )
    {
        return await db
            .Users.AsNoTracking()
            .Where(user => user.Id == id)
            .Select(selector)
            .SingleOrDefaultAsync();
    }

    public Task<bool> ExistsByEmailAsync(string email) =>
        db.Users.AnyAsync(u => u.Email == email.Trim().ToLower());

    public async Task AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }
}
