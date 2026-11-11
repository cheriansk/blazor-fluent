using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Transactions;

public class UnitOfWorkDomainEventsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<ITenantContext> _tenantContext;
    private readonly Mock<ICurrentUser> _currentUser;
    private readonly Guid _tenantId = Guid.NewGuid();

    private record TestDomainEvent(string Note) : DomainEventBase;

    public UnitOfWorkDomainEventsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        _dateTimeProvider = new Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTime.UtcNow);
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _tenantContext = new Mock<ITenantContext>();
        _tenantContext.Setup(t => t.HasActiveTenant).Returns(true);
        _tenantContext.Setup(t => t.TenantId).Returns(_tenantId);

        _currentUser = new Mock<ICurrentUser>();
        _currentUser.Setup(c => c.UserId).Returns("test-user");
        _currentUser.Setup(c => c.Email).Returns("test@example.com");

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new AppDbContext(_options, _dateTimeProvider.Object, _tenantContext.Object, _currentUser.Object);
        initContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenSucceeds_DispatchesHarvestedAndSuccessDomainEvents()
    {
        // Arrange
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(_options, _dateTimeProvider.Object, _tenantContext.Object, _currentUser.Object));

        var auditService = new Mock<IAuditService>();
        var domainEventDispatcher = new Mock<IDomainEventDispatcher>();
        var dispatchedEvents = new List<IDomainEvent>();
        domainEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => dispatchedEvents.Add(e))
            .Returns(Task.CompletedTask);

        var uow = new UnitOfWork(
            dbFactory.Object,
            auditService.Object,
            domainEventDispatcher.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<UnitOfWork>.Instance);

        // Act
        var result = await uow.ExecuteInTransactionAsync(async (scope, ct) =>
        {
            var program = new ProgramEntity
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = "Test Program",
                Code = "TEST",
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddMonths(3),
                Status = ProgramStatusEnum.Active
            };
            program.RaiseDomainEvent(new TestDomainEvent("Program created in test"));

            scope.StageInsert(program);
            return await Task.FromResult(Result.Success());
        }, "TestOperation");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(dispatchedEvents, e => e is TestDomainEvent t && t.Note == "Program created in test");
        Assert.Contains(dispatchedEvents, e => e is UnitOfWorkSucceededDomainEvent s && s.OperationName == "TestOperation");
        Assert.DoesNotContain(dispatchedEvents, e => e is UnitOfWorkFailedDomainEvent);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenFails_ClearsEntityEvents_AndDispatchesFailedDomainEvent()
    {
        // Arrange
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(_options, _dateTimeProvider.Object, _tenantContext.Object, _currentUser.Object));

        var auditService = new Mock<IAuditService>();
        var domainEventDispatcher = new Mock<IDomainEventDispatcher>();
        var dispatchedEvents = new List<IDomainEvent>();
        domainEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => dispatchedEvents.Add(e))
            .Returns(Task.CompletedTask);

        var uow = new UnitOfWork(
            dbFactory.Object,
            auditService.Object,
            domainEventDispatcher.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<UnitOfWork>.Instance);

        // Act
        var result = await uow.ExecuteInTransactionAsync(async (scope, ct) =>
        {
            var program = new ProgramEntity
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = "Aborted Program",
                Code = "ABORT",
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddMonths(3),
                Status = ProgramStatusEnum.Active
            };
            program.RaiseDomainEvent(new TestDomainEvent("Should not be published"));

            scope.StageInsert(program);
            return await Task.FromResult(Result.Failure("Business rule rejection"));
        }, "FailingOperation");

        // Assert
        Assert.False(result.Succeeded);
        Assert.DoesNotContain(dispatchedEvents, e => e is TestDomainEvent);
        Assert.DoesNotContain(dispatchedEvents, e => e is UnitOfWorkSucceededDomainEvent);
        Assert.Contains(dispatchedEvents, e => e is UnitOfWorkFailedDomainEvent f && f.OperationName == "FailingOperation" && f.Errors.Contains("Business rule rejection"));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithIntermediateSave_SynthesizesRegistryEvents_AndDispatchesOnSuccess()
    {
        // Arrange
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(_options, _dateTimeProvider.Object, _tenantContext.Object, _currentUser.Object));

        var auditService = new Mock<IAuditService>();
        var domainEventDispatcher = new Mock<IDomainEventDispatcher>();
        var dispatchedEvents = new List<IDomainEvent>();
        domainEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => dispatchedEvents.Add(e))
            .Returns(Task.CompletedTask);

        var uow = new UnitOfWork(
            dbFactory.Object,
            auditService.Object,
            domainEventDispatcher.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<UnitOfWork>.Instance);

        // Act
        var result = await uow.ExecuteInTransactionAsync(async (scope, ct) =>
        {
            var db = ((TransactionContext)scope).DbContext;
            var program = new ProgramEntity
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = "Auto Program",
                Code = "AUTO",
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddMonths(3),
                Status = ProgramStatusEnum.Active
            };
            db.Programs.Add(program);
            db.IsTransactionScopeCommitAuthorized = true;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            finally
            {
                db.IsTransactionScopeCommitAuthorized = false;
            }

            return await Task.FromResult(Result.Success());
        }, "AutoRegistryOperation");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(dispatchedEvents, e => e is ProgramCreatedDomainEvent p && p.Code == "AUTO");
        Assert.Contains(dispatchedEvents, e => e is UnitOfWorkSucceededDomainEvent s && s.OperationName == "AutoRegistryOperation" && s.AffectedEntities.Any(a => a.Contains("ProgramEntity")));
    }
}
