using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Interceptors;
using BlazorEnterprise.Persistence.Services;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Interceptors;

public class TransactionScopeEnforcementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IDateTimeProvider> _mockTime;
    private readonly Mock<ITenantContext> _mockTenant;
    private readonly Mock<ICurrentUser> _mockUser;

    public TransactionScopeEnforcementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        _mockTime = new Mock<IDateTimeProvider>();
        _mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);
        _mockTime.Setup(t => t.UseUtc).Returns(true);

        _mockTenant = new Mock<ITenantContext>();
        _mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        _mockTenant.Setup(t => t.HasActiveTenant).Returns(true);

        _mockUser = new Mock<ICurrentUser>();
        _mockUser.Setup(u => u.UserId).Returns("test_user_id");
        _mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var guardInterceptor = new TransactionScopeGuardInterceptor();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(guardInterceptor)
            .Options;

        var mockDbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        mockDbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(_options, _mockTime.Object, _mockTenant.Object, _mockUser.Object));
        mockDbFactory.Setup(f => f.CreateDbContext())
            .Returns(() => new AppDbContext(_options, _mockTime.Object, _mockTenant.Object, _mockUser.Object));

        _dbFactory = mockDbFactory.Object;

        using var initContext = new AppDbContext(_options, _mockTime.Object, _mockTenant.Object, _mockUser.Object);
        initContext.Database.EnsureCreated();
        initContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task SelectQueries_ExecuteFreely_WithoutTransactionScope()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        
        // Act
        var act = async () => await db.Programs.AsNoTracking().ToListAsync();

        // Assert - SELECT must run with zero transaction requirement
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DirectSaveChanges_WithMutations_OutsideTransactionScope_ThrowsInvalidOperationException()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var program = new ProgramEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Unscoped Program",
            Code = "UNSCOPED",
            Status = ProgramStatusEnum.Active,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddMonths(3),
            Created = DateTime.UtcNow,
            CreatedBy = "Tester",
            Updated = DateTime.UtcNow
        };

        db.Programs.Add(program);

        // Act & Assert - direct SaveChangesAsync without scope must fail closed
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TRANSACTION SCOPE VIOLATION*");
    }

    [Fact]
    public async Task StageInsert_InsideTransactionScope_SavesSuccessfully()
    {
        await using var scope = await AmbientTransactionContext.AcquireWriteScopeAsync(_dbFactory, CancellationToken.None, "TestInsert");
        
        var program = new ProgramEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Scoped Program",
            Code = "SCOPED",
            Status = ProgramStatusEnum.Active,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddMonths(3),
            Created = DateTime.UtcNow,
            CreatedBy = "Tester",
            Updated = DateTime.UtcNow
        };

        scope.StageInsert(program);

        // Act
        var act = async () => await scope.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StageUpdate_DetectsConcurrencyConflict_WhenVersionMismatches()
    {
        await using var scope = await AmbientTransactionContext.AcquireWriteScopeAsync(_dbFactory, CancellationToken.None, "TestConcurrency");

        var program = new ProgramEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Existing Program",
            Code = "EXISTING",
            Status = ProgramStatusEnum.Active,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddMonths(3),
            Version = 505u, // In-memory/tracked version
            Created = DateTime.UtcNow,
            CreatedBy = "Tester",
            Updated = DateTime.UtcNow
        };

        scope.StageInsert(program);
        await scope.SaveChangesAsync();

        // Attempt to stage an update asserting an outdated version (500 vs 505)
        var act = () => scope.StageUpdate(program, expectedVersion: 500u);

        // Assert - Concurrency conflict must trigger immediately
        act.Should().Throw<DbUpdateConcurrencyException>()
            .WithMessage("*Concurrency Conflict*Expected version was 500*tracked version is 505*");
    }
}
