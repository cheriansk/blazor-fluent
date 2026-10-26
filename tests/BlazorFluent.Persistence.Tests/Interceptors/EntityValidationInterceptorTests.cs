using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Exceptions;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class EntityValidationInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EntityValidationInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static TaskEntity CreateTestTask(string title) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Title = title,
        Description = "Validation test description",
        Priority = TaskPriorityEnum.Medium,
        Status = TaskStatusEnum.Open,
        Level = TaskLevelEnum.Project,
        CreatedBy = "tester@example.com",
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7)
    };

    public class AlwaysFailTaskValidator : AbstractValidator<TaskEntity>
    {
        public AlwaysFailTaskValidator()
        {
            RuleFor(x => x.Title).Must(_ => false).WithMessage("Title validation failure for testing.");
        }
    }

    public class AlwaysPassTaskValidator : AbstractValidator<TaskEntity>
    {
        public AlwaysPassTaskValidator()
        {
            RuleFor(x => x.Title).NotEmpty();
        }
    }

    private AppDbContext CreateContext(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        var serviceProvider = services.BuildServiceProvider();

        var interceptor = new EntityValidationInterceptor(serviceProvider);

        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns("user_1");
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        return context;
    }

    [Fact]
    public async Task SaveChangesAsync_WhenValidatorPasses_Succeeds()
    {
        using var context = CreateContext(services =>
        {
            services.AddSingleton<IValidator<TaskEntity>, AlwaysPassTaskValidator>();
        });

        var task = CreateTestTask("Valid Task");
        context.Tasks.Add(task);

        var result = await context.SaveChangesAsync();
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenValidatorFails_ThrowsEntityValidationException()
    {
        using var context = CreateContext(services =>
        {
            services.AddSingleton<IValidator<TaskEntity>, AlwaysFailTaskValidator>();
        });

        var task = CreateTestTask("Invalid Task");
        context.Tasks.Add(task);

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<EntityValidationException>()
            .Where(e => e.Errors.ContainsKey("TaskEntity.Title"));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNoValidatorRegistered_ThrowsFailClosedException()
    {
        using var context = CreateContext();

        var task = CreateTestTask("Unvalidated Task");
        context.Tasks.Add(task);

        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<EntityValidationException>()
            .Where(e => e.Errors.ContainsKey("TaskEntity.Validator"));
    }
}
