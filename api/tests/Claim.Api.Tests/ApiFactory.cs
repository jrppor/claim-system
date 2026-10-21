using Claim.Application.Auth;
using Claim.Application.Health;
using Claim.Domain.Users;
using Claim.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claim.Api.Tests;

public sealed class FakeDatabaseProbe : IDatabaseProbe
{
    public bool IsUp { get; set; } = true;
    public bool Throws { get; set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (Throws) throw new InvalidOperationException("boom: secret connection detail");
        return Task.FromResult(IsUp);
    }
}

public sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public IReadOnlyList<User> Users => _users;

    public User Add(string email, string password, string role, bool isActive = true)
    {
        var user = new User(_users.Count + 1, email, new Pbkdf2PasswordHasher().Hash(password), email.Split('@')[0], role, isActive);
        _users.Add(user);
        return user;
    }

    public void Deactivate(string email)
    {
        var i = _users.FindIndex(u => u.Email == email);
        _users[i] = _users[i] with { IsActive = false };
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> FindByIdAsync(long id, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<User>>(_users.ToList());
}

public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly Dictionary<string, long> _tokens = [];
    private readonly object _lock = new();

    public Task SaveAsync(string token, long userId, TimeSpan lifetime, CancellationToken ct)
    {
        lock (_lock) _tokens[token] = userId;
        return Task.CompletedTask;
    }

    public Task<long?> ConsumeAsync(string token, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult(_tokens.Remove(token, out var id) ? (long?)id : null);
    }

    public Task RevokeAsync(string token, CancellationToken ct)
    {
        lock (_lock) _tokens.Remove(token);
        return Task.CompletedTask;
    }
}

/// <summary>Test host: real pipeline, in-memory fakes in place of infrastructure.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rd!demo";

    public FakeDatabaseProbe Database { get; } = new();
    public FakeUserRepository Users { get; } = new();
    public FakeRefreshTokenStore RefreshTokens { get; } = new();

    public ApiFactory()
    {
        Users.Add("staff@claim.test", Password, Role.GarageStaff);
        Users.Add("adjuster@claim.test", Password, Role.InsuranceAdjuster);
        Users.Add("admin@claim.test", Password, Role.Admin);
        Users.Add("former@claim.test", Password, Role.GarageStaff, isActive: false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseProbe>();
            services.AddSingleton<IDatabaseProbe>(Database);
            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(Users);
            services.RemoveAll<IRefreshTokenStore>();
            services.AddSingleton<IRefreshTokenStore>(RefreshTokens);
        });
    }
}
