
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;

namespace MyBlazorApp1.Services;

public class OperatorAuthService
{
    private readonly IDbContextFactory<DiagDbContext> _dbFactory;
    private readonly PasswordHasher<OperatorAccount> _passwordHasher = new();

    public OperatorAuthService(
        IDbContextFactory<DiagDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var account = await db.OperatorAccounts
            .SingleOrDefaultAsync(x => x.Username == username);

        if (account == null || !account.IsActive)
            return false;

        var result = _passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            password);

        return result != PasswordVerificationResult.Failed;
    }

    public async Task<bool> EnsureInitialOperatorAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Never overwrite or recreate accounts automatically.
        if (await db.OperatorAccounts.AnyAsync())
            return false;

        var account = new OperatorAccount
        {
            Username = username.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        account.PasswordHash = _passwordHasher.HashPassword(
            account,
            password);

        db.OperatorAccounts.Add(account);

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // For example, a duplicate username or concurrent startup.
            return false;
        }
    }



    public async Task<bool> ResetPasswordAsync(
        string username,
        string newPassword)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var account = await db.OperatorAccounts
            .SingleOrDefaultAsync(x => x.Username == username.Trim());

        if (account == null)
            return false;

        account.PasswordHash = _passwordHasher.HashPassword(
            account,
            newPassword);

        await db.SaveChangesAsync();
        return true;
    }

}
