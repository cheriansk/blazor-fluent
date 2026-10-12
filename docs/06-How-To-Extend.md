# 06. How-to-Extend Recipes & Code Patterns

> **Purpose**: Practical, step-by-step implementation guides and code skeletons demonstrating how to add new entities, services, pages, background jobs, and wizards to the solution.  
> **Who should read this**: Developers implementing new business features, integrations, or workflows on top of this template.

---

## 🍳 Recipe 1: Adding a New Domain Entity

Follow these 5 steps to add a new database-backed domain entity:

### Step 1: Create the Entity in `BlazorFluent.Core`
Create `src/BlazorFluent.Core/Domain/Orders/PurchaseOrderEntity.cs`:

```csharp
using BlazorFluent.Core.Abstractions;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Orders;

/// <summary>
/// Purchase order scoped strictly to a tenant and project.
/// </summary>
public class PurchaseOrderEntity : TenantAuditableEntity, IProjectScopedEntity, ISoftDeletableEntity
{
    public Guid ProjectId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Open;

    // Soft delete contract
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
```

### Step 2: Configure Fluent API Mapping in `BlazorFluent.Persistence`
Create `src/BlazorFluent.Persistence/Configurations/Orders/PurchaseOrderEntityConfiguration.cs`:

```csharp
using BlazorFluent.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Orders;

public class PurchaseOrderEntityConfiguration : IEntityTypeConfiguration<PurchaseOrderEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderEntity> builder)
    {
        builder.ToTable("PurchaseOrders", "orders");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.OrderNumber }).IsUnique();
    }
}
```

### Step 3: Add `DbSet` to `AppDbContext.cs`
In `src/BlazorFluent.Persistence/Context/AppDbContext.cs`:

```csharp
public DbSet<PurchaseOrderEntity> PurchaseOrders => Set<PurchaseOrderEntity>();
```

### Step 4: Add Tier 2 Entity Validator in `BlazorFluent.Core`
Create `src/BlazorFluent.Core/Domain/Orders/PurchaseOrderEntityValidator.cs`:

```csharp
using FluentValidation;

namespace BlazorFluent.Core.Domain.Orders;

public class PurchaseOrderEntityValidator : AbstractValidator<PurchaseOrderEntity>
{
    public PurchaseOrderEntityValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty().WithMessage("Order number is required.")
            .MaximumLength(64);

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Total amount cannot be negative.");
    }
}
```

### Step 5: Generate and Apply EF Core Migration
```bash
dotnet ef migrations add AddPurchaseOrders --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web --output-dir Migrations
dotnet ef database update --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web
```

---

## 🍳 Recipe 2: Adding a Service (Contract + Impl + DI)

### Step 1: Define the Contract in `BlazorFluent.Core`
Create `src/BlazorFluent.Core/Contracts/IPurchaseOrderService.cs`:

```csharp
using BlazorFluent.Core.Domain.Orders;

namespace BlazorFluent.Core.Contracts;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderEntity> CreateOrderAsync(Guid projectId, string orderNumber, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderEntity>> GetOrdersByProjectAsync(Guid projectId, CancellationToken ct = default);
}
```

### Step 2: Implement Service in `BlazorFluent.Persistence`
Create `src/BlazorFluent.Persistence/Services/PurchaseOrderService.cs`:

```csharp
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Orders;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IProjectAuthorizationService _projectAuth;

    public PurchaseOrderService(AppDbContext dbContext, IProjectAuthorizationService projectAuth)
    {
        _dbContext = dbContext;
        _projectAuth = projectAuth;
    }

    public async Task<PurchaseOrderEntity> CreateOrderAsync(Guid projectId, string orderNumber, decimal amount, CancellationToken ct = default)
    {
        // Service-layer Zero-Trust write gate
        await _projectAuth.EnsureCanEditAsync(projectId, "CreatePurchaseOrder");

        var order = new PurchaseOrderEntity
        {
            ProjectId = projectId,
            OrderNumber = orderNumber.Trim(),
            TotalAmount = amount
        };

        _dbContext.PurchaseOrders.Add(order);
        await _dbContext.SaveChangesAsync(ct);
        return order;
    }

    public async Task<IReadOnlyList<PurchaseOrderEntity>> GetOrdersByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        // Service-layer Zero-Trust read gate
        await _projectAuth.EnsureCanVisitAsync(projectId, "GetPurchaseOrders");

        return await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.Created)
            .ToListAsync(ct);
    }
}
```

### Step 3: Register in `PersistenceExtensions.cs`
In `src/BlazorFluent.Persistence/PersistenceExtensions.cs`:

```csharp
services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
```

---

## 🍳 Recipe 3: Adding a Routable Page (`*Page.razor`)

Create `src/BlazorFluent.Web/Modules/Orders/Pages/PurchaseOrdersPage.razor`:

```razor
@page "/projects/{ProjectId:guid}/orders"
@attribute [Authorize]
@rendermode InteractiveServer
@using TaskStatus = BlazorFluent.Core.DataListTypes.TaskStatus
@inject IPurchaseOrderService OrderService
@inject NavigationManager Navigation

<PageTitle>Project Purchase Orders</PageTitle>

<div class="page-container">
    <PageHeaderComp Title="Purchase Orders" 
                    Subtitle="Manage procurement work items for this project">
        <ActionContent>
            <FluentButton Appearance="ButtonAppearance.Primary" 
                          IconStart="@(new Icons.Regular.Size16.Add())"
                          OnClick="@OpenCreateModal">
                New Order
            </FluentButton>
        </ActionContent>
    </PageHeaderComp>

    @if (_isLoading)
    {
        <FluentSpinner Size="@SpinnerSize.Medium" />
    }
    else if (_orders.Count == 0)
    {
        <EmptyStateComp Title="No Purchase Orders" 
                        Description="No orders found for this project." />
    }
    else
    {
        <FluentDataGrid Items="@_orders.AsQueryable()">
            <PropertyColumn Property="@(x => x.OrderNumber)" Title="Order #" Sortable="true" />
            <PropertyColumn Property="@(x => x.TotalAmount)" Title="Amount" Format="C2" Sortable="true" />
            <TemplateColumn Title="Date">
                <UserDateTime Value="@context.Created" Format="yyyy-MM-dd" />
            </TemplateColumn>
        </FluentDataGrid>
    }
</div>

@code {
    [Parameter]
    public Guid ProjectId { get; set; }

    private bool _isLoading = true;
    private List<PurchaseOrderEntity> _orders = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _orders = (await OrderService.GetOrdersByProjectAsync(ProjectId)).ToList();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenCreateModal() { /* Open modal */ }
}
```

---

## 🍳 Recipe 4: Adding a Background Batch Job

### Step 1: Define the Job Event in `BlazorFluent.Core`
Create `src/BlazorFluent.Core/Events/OrderSyncJobEvent.cs`:

```csharp
namespace BlazorFluent.Core.Events;

public record OrderSyncJobEvent(string TenantId, Guid OrderId) : BaseJobEvent(TenantId);
```

### Step 2: Implement the Handler in `BlazorFluent.Jobs`
Create `src/BlazorFluent.Jobs/Jobs/Orders/OrderSyncJobHandler.cs`:

```csharp
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Orders;

public class OrderSyncJobHandler : IBatchJobHandler<OrderSyncJobEvent>
{
    private readonly ILogger<OrderSyncJobHandler> _logger;

    public OrderSyncJobHandler(ILogger<OrderSyncJobHandler> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(OrderSyncJobEvent jobEvent, CancellationToken ct)
    {
        _logger.LogInformation("Synchronizing order {OrderId} for tenant {TenantId}...", jobEvent.OrderId, jobEvent.TenantId);
        // Process order synchronization logic...
        await Task.Delay(100, ct);
    }
}
```

> Handlers implementing `IBatchJobHandler<T>` are **auto-discovered** during startup assembly scanning in `JobsExtensions.cs`. Zero manual DI registration is required!

### Step 3: Trigger the Job from UI or Services
```csharp
await _jobQueue.EnqueueAsync(new OrderSyncJobEvent(tenantId, order.Id));
```

---

## 🍳 Recipe 5: Adding a Wizard Modal (`FluentWizardModal`)

To compose a multi-step modal wizard using `BlazorFluent.UI.Templates`:

```razor
<FluentWizardModal TState="CreateOrderReqDto"
                   IsOpen="@_isOpen"
                   IsOpenChanged="@(val => _isOpen = val)"
                   Title="New Purchase Order"
                   InitialState="@_orderDto"
                   OnFinish="@HandleFinishAsync"
                   OnCancel="@(() => _isOpen = false)">
    <Steps>
        <WizardStep Title="General Details" OnValidateAsync="@ValidateStep1Async">
            <FluentTextInput Label="Order Number *" @bind-Value="_orderDto.OrderNumber" />
            <FluentNumberInput Label="Amount *" @bind-Value="_orderDto.Amount" />
        </WizardStep>

        <WizardStep Title="Approvals" OnValidateAsync="@ValidateStep2Async">
            <FluentTextInput Label="Approver Email *" @bind-Value="_orderDto.ApproverEmail" />
        </WizardStep>
    </Steps>
</FluentWizardModal>

@code {
    private bool _isOpen;
    private CreateOrderReqDto _orderDto = new();

    private Task<bool> ValidateStep1Async() =>
        Task.FromResult(!string.IsNullOrWhiteSpace(_orderDto.OrderNumber) && _orderDto.Amount > 0);

    private Task<bool> ValidateStep2Async() =>
        Task.FromResult(!string.IsNullOrWhiteSpace(_orderDto.ApproverEmail) && _orderDto.ApproverEmail.Contains('@'));

    private async Task HandleFinishAsync(CreateOrderReqDto state)
    {
        await OrderService.CreateOrderAsync(state.ProjectId, state.OrderNumber, state.Amount);
        _isOpen = false;
    }
}
```

---

## 🔗 Related Documentation

- [00. Overview & Glossary](00-Overview-and-Glossary.md) — Personas, routes, and domain glossary.
- [01. Getting Started Guide](01-Getting-Started.md) — Local setup, migrations, and troubleshooting.
- [02. Architecture Deep Dive](02-Architecture.md) — Layer boundaries, DI rules, and caching topology.
- [03. Security Model](03-Security-Model.md) — Multi-tenant write barriers and role gates.
- [04. Task Hierarchy & Workflows](04-Task-Hierarchy-and-Workflows.md) — Workflow orchestration and wizard models.
- [05. UI Conventions & Fluent V5](05-UI-Conventions-Fluent-V5.md) — Component naming taxonomy and V5 guidelines.
- [07. AI Agent Operating Rules](07-AI-Agent-Rules.md) — Operational guidelines for AI coding assistants.
