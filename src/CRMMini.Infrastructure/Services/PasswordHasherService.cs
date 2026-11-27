namespace CRMMini.Infrastructure.Services;

using CRMMini.Application.Interfaces;
using CRMMini.Domain.Entities;
using Microsoft.AspNetCore.Identity;

public sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password)
        => _hasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string passwordHash, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, passwordHash, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
