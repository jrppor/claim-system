using Claim.Application.Auth;
using Claim.Application.Claims;
using Claim.Application.Health;
using Claim.Domain.Users;
using Claim.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Claim.Api.Tests;

/// <summary>A clock the test moves by hand, so retry back-off needs no real waiting.</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly object _lock = new();

    public override DateTimeOffset GetUtcNow() { lock (_lock) return _now; }

    public void Advance(TimeSpan by) { lock (_lock) _now += by; }
}

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

    public Task<User?> CreateAsync(string email, string passwordHash, string displayName, string role, CancellationToken ct)
    {
        lock (_users)
        {
            if (_users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<User?>(null);
            var user = new User(_users.Count + 1, email, passwordHash, displayName, role, true);
            _users.Add(user);
            return Task.FromResult<User?>(user);
        }
    }

    public Task<bool> SetPasswordHashAsync(long id, string passwordHash, CancellationToken ct)
    {
        var i = _users.FindIndex(u => u.Id == id);
        if (i >= 0) _users[i] = _users[i] with { PasswordHash = passwordHash };
        return Task.FromResult(i >= 0);
    }

    public Task<bool> SetActiveAsync(long id, bool isActive, CancellationToken ct)
    {
        var i = _users.FindIndex(u => u.Id == id);
        if (i >= 0) _users[i] = _users[i] with { IsActive = isActive };
        return Task.FromResult(i >= 0);
    }

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<User>>(_users.ToList());
}

public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly Dictionary<string, long> _tokens = [];
    private readonly object _lock = new();

    public bool Contains(string token)
    {
        lock (_lock) return _tokens.ContainsKey(token);
    }

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

    public Task RevokeAllForUserAsync(long userId, CancellationToken ct)
    {
        lock (_lock)
            foreach (var t in _tokens.Where(kv => kv.Value == userId).Select(kv => kv.Key).ToList()) _tokens.Remove(t);
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string token, CancellationToken ct)
    {
        lock (_lock) _tokens.Remove(token);
        return Task.CompletedTask;
    }
}

/// <summary>Test host: real pipeline, in-memory fakes in place of infrastructure.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rd!demo";

    public FakeDatabaseProbe Database { get; } = new();
    public FakeUserRepository Users { get; } = new();
    public FakeRefreshTokenStore RefreshTokens { get; } = new();
    public FakePolicyRepository Policies { get; } = new();
    public FakeClaimRepository Claims { get; } = new();
    public FakeClaimHistoryRepository History { get; } = new();
    public FakeEmailOutbox Outbox { get; } = new();
    public FakeEmailSender Mailbox { get; } = new();
    public FakeRepairEstimateRepository Estimates { get; } = new();
    public FakeInvoiceRepository Invoices { get; } = new();
    public FakeClaimPhotoRepository Photos { get; } = new();
    public FakeFileStorage Files { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; }

    /// <summary>True: thumbnails are made by a fake. A subclass returns false to run the real image library.</summary>
    protected virtual bool FakeThumbnails => true;
    public ManualClock Clock { get; } = new();

    /// <summary>
    /// One pass of the email sender, run by the test instead of by the background worker (which the test host does not start),
    /// so what the pass sees depends only on <see cref="Clock"/>.
    /// </summary>
    public Task<int> DispatchEmailsAsync() => Services.GetRequiredService<EmailOutboxDispatcher>().DispatchDueAsync(default);

    public ApiFactory()
    {
        UnitOfWork = new FakeUnitOfWork(Claims, History, Outbox, Estimates, Invoices, Photos);
        Users.Add("staff@claim.test", Password, Role.GarageStaff);
        Users.Add("adjuster@claim.test", Password, Role.InsuranceAdjuster);
        Users.Add("admin@claim.test", Password, Role.Admin);
        Users.Add("former@claim.test", Password, Role.GarageStaff, isActive: false);
    }

    /// <summary>A client that reads a Claim and sends its version before changing it, like the UI.</summary>
    public HttpClient CreateVersionedClient() => CreateDefaultClient(new AutoVersionHandler());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IDatabaseProbe>();
            services.AddSingleton<IDatabaseProbe>(Database);
            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(Users);
            services.RemoveAll<IRefreshTokenStore>();
            services.AddSingleton<IRefreshTokenStore>(RefreshTokens);
            services.ReplaceClaimRepositories(Policies, Claims);
            services.RemoveAll<IClaimPhotoRepository>();
            services.AddSingleton<IClaimPhotoRepository>(Photos);
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(Files);
            if (FakeThumbnails)
            {
                services.RemoveAll<IThumbnailGenerator>();
                services.AddSingleton<IThumbnailGenerator, FakeThumbnailGenerator>();
            }
            services.ReplaceWorkflowStores(History, UnitOfWork, Outbox, Mailbox, Estimates, Invoices);
        });
    }
}
