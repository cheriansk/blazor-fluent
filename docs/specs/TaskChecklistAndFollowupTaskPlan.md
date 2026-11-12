# Task Checklist & FollowupTask Implementation Plan

**Document Status**: Reconstructed from code analysis and architecture review  
**Last Updated**: 2024  
**Complexity**: Large (Multiple entities, DB schema changes, service layer)

---

## Executive Summary

This plan outlines the implementation of an **ActionItem** (task checklist) system integrated with the task hierarchy, combined with a semantic rename of **MiscTask → FollowupTask** for clarity. The system supports:

- ✅ Direct ActionItems attached to tasks (RAID items, FollowupTasks)
- ✅ Grouped ActionItems nested under SubTasks (SprintTask organization)
- ✅ Blocking/Dependency tracking at SubTask level
- ✅ Hierarchical completion and complexity rollup calculations
- ✅ File-level organization via SubTask grouping

---

## 1. Architecture & Design Decisions

### ActionItemEntity Design Philosophy

**Flexible Attachment Strategy**:
- **Direct Attachment** (no SubTask intermediary): 
  - RAID items (Risk, Assumption, Issue)
  - FollowupTasks
  - Lightweight action tracking
  
- **Grouped Attachment** (under SubTask):
  - SprintTasks with SubTasks
  - File-level organization
  - Dependency tracking at SubTask level

**Use Cases**:
```
RAID Item → [ActionItems] (direct)
FollowupTask → [ActionItems] (direct)
SprintTask → [SubTask] → [ActionItems] (grouped)
```

### MiscTask → FollowupTask Rename

**Rationale**:
- **MiscTask** is ambiguous (could mean anything miscellaneous)
- **FollowupTask** clearly describes developer tasks, code reviews, and followup items
- Semantic clarity for stakeholders and developers
- Better communicates purpose: action items requiring developer attention

---

## 2. Data Model Specification

### 2.1 New: ActionItemEntity

**File**: `src/algration/command_control/Core/Domain/Tasks/ActionItemEntity.cs`

```csharp
public class ActionItemEntity : ProjectScopedEntity
{
    // Required: Task reference
    public Guid ParentTaskId { get; set; }
    public virtual BaseTaskEntity ParentTask { get; set; }
    
    // Optional: SubTask grouping (for SprintTask organization)
    public Guid? ParentSubTaskId { get; set; }
    public virtual SubTaskEntity ParentSubTask { get; set; }
    
    // Core Fields
    public string Title { get; set; } // 1-500 chars
    public string Description { get; set; } // Optional, max 2000
    
    // Status & Tracking
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;
    public int Complexity { get; set; } // 1-5 points
    public decimal EstimatedHours { get; set; } = 0;
    public int OrderIndex { get; set; } // For ordering within SubTask or task level
    
    // Blocking/Dependency
    public bool IsBlocked { get; set; } = false;
    public string BlockingReason { get; set; } // min 10 chars if IsBlocked=true
    
    // Audit
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid? ModifiedBy { get; set; }
}
```

**Enums**:
```csharp
public enum ActionItemStatus
{
    Open,
    InProgress,
    Completed,
    Blocked,
    Deferred
}

public enum ActionItemComplexity
{
    Low = 1,
    Medium = 3,
    High = 5
}
```

**Interfaces**:
- Implements `IHierarchyEntity` (action items don't need scenario versioning)
- Navigation: ParentTask (BaseTaskEntity), ParentSubTask (SubTaskEntity optional)

---

### 2.2 Modified: MiscTask → FollowupTask

**File**: Rename `src/algration/command_control/Core/Domain/Tasks/MiscTaskEntity.cs` 
**To**: `src/algration/command_control/Core/Domain/Tasks/FollowupTaskEntity.cs`

**Changes**:
```csharp
// OLD
public class MiscTaskEntity : BaseTaskEntity { }

// NEW
public class FollowupTaskEntity : BaseTaskEntity 
{
    // Inherits all BaseTaskEntity fields
    // Semantic: Developer followup, review, or action task
    // Use Case: Code reviews, bug fixes, technical debt, action items
}
```

**Documentation Update**:
- XML Comment: "ed-hoc or backlog task" → "developer followup, review, or action task"
- Use Case: Developer tasks, code reviews, followup items with action item checklists

---

### 2.3 Database Configuration

**File**: `src/algration/command_control/Persistence/Configurations/Tasks/ActionItemsEntityConfiguration.cs`

```csharp
protected override void Configure(EntityTypeBuilder<ActionItemEntity> builder)
{
    builder.ToTable("ActionItems", schema: "tasks");
    
    builder.HasKey(x => new { x.Id, x.ProjectId });
    
    // Foreign Keys
    builder.HasOne(x => x.ParentTask)
        .WithMany() // Navigation not configured on BaseTaskEntity
        .HasForeignKey(x => x.ParentTaskId)
        .OnDelete(DeleteBehavior.Cascade);
    
    builder.HasOne(x => x.ParentSubTask)
        .WithMany() // Navigation on SubTaskEntity
        .HasForeignKey(x => new { x.ParentSubTaskId, x.ProjectId })
        .OnDelete(DeleteBehavior.Cascade)
        .IsRequired(false);
    
    // Columns
    builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
    builder.Property(x => x.Description).HasMaxLength(2000);
    builder.Property(x => x.BlockingReason).HasMaxLength(1000);
    builder.Property(x => x.Status).HasConversion<string>();
    
    // Indexes
    builder.HasIndex(x => new { x.ParentTaskId, x.ProjectId });
    builder.HasIndex(x => new { x.ParentSubTaskId, x.ProjectId, x.OrderIndex });
    builder.HasIndex(x => new { x.ProjectId, x.Status });
}
```

**Constraints**:
- Composite PK: (Id, ProjectId) - ProjectScopedEntity pattern
- Required FK: ParentTaskId (cascade delete)
- Optional FK: ParentSubTaskId (cascade delete if set)
- Column Length: Title (500), Description (2000), BlockingReason (1000)
- Indexes on (ParentTaskId), (ParentSubTaskId, OrderIndex), (ProjectId, Status)

---

## 3. Database Migration

### Migration File

**Generate**:
```powershell
dotnet ef migrations add AddActionItems_And_Rename_MiscTask `
  --startup-project src/algration.command_control.AppHost `
  --project src/algration.command_control.Persistence
```

**Migration will**:
1. Create `tasks.ActionItems` table with schema above
2. Rename `tasks.MiscTasks` → `tasks.FollowupTasks`
3. Update all foreign key references
4. Create indexes
5. Update data (if any existing MiscTask references)

---

## 4. Service Layer Implementation

### 4.1 Service Contract

**File**: `src/algration/command_control/Core/Contracts/IActionItemService.cs`

```csharp
public interface IActionItemService
{
    // Create
    Task<ActionItemDto> CreateActionItemAsync(Guid taskId, CreateActionItemDto dto);
    Task<ActionItemDto> CreateActionItemUnderSubTaskAsync(
        Guid taskId, Guid subTaskId, CreateActionItemDto dto);
    
    // Update
    Task<ActionItemDto> UpdateActionItemAsync(Guid actionItemId, UpdateActionItemDto dto);
    Task<ActionItemDto> MoveActionItemAsync(Guid actionItemId, Guid? newSubTaskId);
    Task<ActionItemDto> ReorderActionItemAsync(Guid actionItemId, int newOrderIndex);
    
    // Status & Blocking
    Task<ActionItemDto> UpdateStatusAsync(Guid actionItemId, ActionItemStatus status);
    Task<ActionItemDto> ToggleBlockingAsync(
        Guid actionItemId, bool isBlocked, string reason = null);
    
    // Query
    Task<IEnumerable<ActionItemDto>> GetActionItemsByTaskAsync(
        Guid taskId, bool includeSubTaskItems = false);
    Task<IEnumerable<ActionItemDto>> GetActionItemsBySubTaskAsync(Guid subTaskId);
    Task<ActionItemDto> GetActionItemAsync(Guid actionItemId);
    Task<IEnumerable<ActionItemDto>> GetBlockedItemsAsync(Guid taskId);
    
    // Delete
    Task DeleteActionItemAsync(Guid actionItemId);
    Task DeleteActionItemsBySubTaskAsync(Guid subTaskId);
    
    // Calculations & Reporting
    Task<int> GetActionItemCountAsync(Guid taskId, bool includeSubTasks = false);
    Task<decimal> GetCompletionPercentageAsync(Guid taskId, bool includeSubTasks = false);
    Task<int> GetBlockedCountAsync(Guid taskId);
    Task<int> GetComplexityTotalAsync(Guid taskId, bool includeSubTasks = false);
}
```

### 4.2 Service Implementation

**File**: `src/algration/command_control/Persistence/Services/ActionItemService.cs`

**Key Methods**:

1. **CreateActionItemAsync** - Direct task attachment
   - Validates TaskId exists
   - Sets ParentSubTaskId = null
   - Assigns OrderIndex = max(existing) + 1
   - Returns created ActionItem

2. **CreateActionItemUnderSubTaskAsync** - Grouped under SubTask
   - Validates TaskId AND SubTaskId
   - Validates SubTask.ParentTaskId matches TaskId
   - Sets ParentSubTaskId = subTaskId
   - Assigns OrderIndex within SubTask

3. **MoveActionItemAsync** - Change grouping
   - From SubTask to Task level (null)
   - From Task to SubTask (set SubTaskId)
   - From SubTask to different SubTask
   - Reorder appropriately

4. **GetActionItemsByTaskAsync** - Query with option
   - `includeSubTasks=false`: Direct items only (ParentSubTaskId IS NULL)
   - `includeSubTasks=true`: All items (direct + nested)

5. **Rollup Calculations**:
   ```csharp
   // % Complete
   decimal completedCount = items.Count(x => x.Status == ActionItemStatus.Completed);
   return (completedCount / items.Count) * 100;
   
   // Complexity Total
   int total = items.Sum(x => x.Complexity);
   
   // Blocked Count
   int blocked = items.Count(x => x.IsBlocked);
   ```

---

## 5. Validation Layer

**File**: `src/algration/command_control/Core/Contracts/ActionItemValidator.cs`

**Validation Rules**:

| Field | Rule | Severity |
|:---|:---|:---:|
| Title | Required, 1-500 chars | Error |
| ParentTaskId | Required, must exist | Error |
| ParentSubTaskId | Optional, if set must exist and match Task | Error |
| Description | Max 2000 chars | Warning |
| Status | Valid enum value | Error |
| Complexity | 1-5 points | Error |
| EstimatedHours | ≥ 0 | Error |
| OrderIndex | ≥ 0 | Error |
| IsBlocked | If true, BlockingReason required (min 10) | Error |

**Custom Rules**:
- If ParentSubTaskId is set, verify SubTask.ParentTaskId == ParentTaskId
- If Status = Completed, ensure no IsBlocked
- If moving between SubTasks, reorder appropriately

---

## 6. UI/Component Layer

### TaskCreationDialog Enhancements

#### Feature 1: File Attachments
- **Drag-and-drop zone** in BaseTaskEntity section
- **File size validation**: Max 10MB per file
- **Uploaded files list** with remove option before submit
- **Storage**: Store in ActionItems or directly on Task based on type

#### Feature 2: Unsaved Changes & Keyboard Shortcuts
- **Unsaved changes modal**: Prompt confirmation when closing with dirty form
- **Esc key**: Cancel dialog without saving
- **Ctrl+Enter**: Submit form immediately

#### Feature 3: Cross-Item Linking & Sub-Task Support

**Sprint Task**:
- Sub-task checkbox (toggle grouping)
- Parent task selector dropdown
- Nested ActionItem list

**Assumption**:
- Multi-select dependency linking to existing project assumptions
- Validation Criteria rich text
- Validation Date picker

**Issue**:
- Multi-select linking to related project issues
- Resolution Steps rich text editor
- Severity level indicator

---

## 7. Query Extensions & Reporting

### Add to ActionItemService

```csharp
// Get item count with optional SubTask inclusion
public async Task<int> GetActionItemCountAsync(
    Guid taskId, bool includeSubTasks = false)
{
    var query = _context.ActionItems
        .Where(x => x.ParentTaskId == taskId);
    
    if (!includeSubTasks)
        query = query.Where(x => x.ParentSubTaskId == null); // Direct only
    
    return await query.CountAsync();
}

// Get completion percentage
public async Task<decimal> GetCompletionPercentageAsync(
    Guid taskId, bool includeSubTasks = false)
{
    var items = await GetActionItemsByTaskAsync(taskId, includeSubTasks);
    if (!items.Any()) return 0;
    
    decimal completed = items.Count(x => x.Status == ActionItemStatus.Completed);
    return (completed / items.Count()) * 100;
}

// Get blocked items
public async Task<int> GetBlockedCountAsync(Guid taskId)
{
    return await _context.ActionItems
        .Where(x => x.ParentTaskId == taskId && x.IsBlocked)
        .CountAsync();
}

// Get complexity total
public async Task<int> GetComplexityTotalAsync(
    Guid taskId, bool includeSubTasks = false)
{
    var query = _context.ActionItems
        .Where(x => x.ParentTaskId == taskId);
    
    if (!includeSubTasks)
        query = query.Where(x => x.ParentSubTaskId == null);
    
    return await query.SumAsync(x => x.Complexity);
}
```

---

## 8. Integration Points

### Dependency Traceability

Update `ProjectileService.cs`:

```csharp
// Get all items with blocking dependencies
public async Task<IEnumerable<ActionItemWithDependencies>> 
    GetActionItemsWithDependenciesAsync(Guid taskId)
{
    // Join ActionItems → SubTask → WorkDependencyEntity
    // Return: ActionItem + SubTask + blocking dependencies
}
```

### Cascade Delete Behavior

**SubTask Deletion**:
- ActionItems with matching ParentSubTaskId cascade delete
- ActionItems with only ParentTaskId (not grouped) unaffected

**Task Deletion**:
- ALL ActionItems cascade delete via ParentTaskId FK
- Includes both direct and SubTask-grouped items

---

## 9. Testing Strategy

### Unit Tests

**File**: `src/algration.command_control.Tests/Domain/Tasks/ActionItemTests.cs`

#### Test Categories

1. **Direct Attachment**
   - ✓ Create ActionItem on RAID item (no SubTask)
   - ✓ ParentSubTaskId remains null
   - ✓ OrderIndex assigned correctly

2. **SubTask-Grouped Attachment**
   - ✓ Create ActionItem under SubTask
   - ✓ Both ParentTaskId and ParentSubTaskId set
   - ✓ Validation: ParentSubTaskId matches Task

3. **Validation**
   - ✓ ParentSubTaskId must reference valid SubTask
   - ✓ SubTask.ParentTaskId must match ActionItem.ParentTaskId
   - ✓ Title required, max 500 chars
   - ✓ IsBlocked requires BlockingReason (min 10 chars)
   - ✓ EstimatedHours ≥ 0

4. **Movement**
   - ✓ Move from SubTask to Task level
   - ✓ Move from Task level to SubTask
   - ✓ Move between different SubTasks
   - ✓ Reordering at both levels

5. **Completion & Rollup**
   - ✓ Direct items completion %
   - ✓ Include SubTasks completion %
   - ✓ Complexity rollup aggregation
   - ✓ Blocked items count

6. **Cascade Delete**
   - ✓ Delete SubTask → ActionItems deleted
   - ✓ Delete Task → ALL ActionItems deleted

---

## 10. Files Checklist

### New Files (10)
- [ ] `ActionItemEntity.cs`
- [ ] `ActionItemValidator.cs`
- [ ] `IActionItemService.cs`
- [ ] `ActionItemService.cs`
- [ ] `ActionItemsEntityConfiguration.cs`
- [ ] `CreateActionItemDto.cs`
- [ ] `UpdateActionItemDto.cs`
- [ ] `ActionItemDto.cs`
- [ ] `AddActionItems_Migration.cs` (auto-generated)
- [ ] `ActionItemTests.cs`

### Modified Files (8)
- [ ] `MiscTaskEntity.cs` → Rename to `FollowupTaskEntity.cs`
- [ ] `BaseTaskEntity.cs` - Add collection navigation
- [ ] `SubTaskEntity.cs` - Add ActionItems collection
- [ ] `AppDbContext.cs` - Register DbSet<ActionItem>
- [ ] `ProjectileService.cs` - Add dependency queries
- [ ] `TaskEntityConfiguration.cs` - Update references
- [ ] All enum/attribute files referencing MiscTask
- [ ] UI components using MiscTask

---

## 11. Implementation Phases

### Phase 1: Data Model (1-2 days)
- [ ] Create ActionItemEntity
- [ ] Create ActionItemsEntityConfiguration
- [ ] Update BaseTaskEntity, SubTaskEntity navigation
- [ ] Generate & execute migration

### Phase 2: Service Layer (2-3 days)
- [ ] Implement IActionItemService
- [ ] Implement ActionItemService
- [ ] Create ActionItemValidator
- [ ] Write unit tests

### Phase 3: UI Components (2-3 days)
- [ ] File attachments dropzone
- [ ] Unsaved changes modal
- [ ] Keyboard shortcuts
- [ ] Cross-item linking UI

### Phase 4: Integration & Reporting (1-2 days)
- [ ] Update ProjectileService
- [ ] Add reporting queries
- [ ] Update dependent pages
- [ ] Integration testing

### Phase 5: Documentation & Polish (1 day)
- [ ] API documentation
- [ ] User guide
- [ ] Known issues/limitations
- [ ] Performance optimization

**Total Estimated**: 7-11 days

---

## 12. Risk & Mitigation

| Risk | Impact | Mitigation |
|:---|:---:|---|
| MiscTask rename breaks existing code | High | Automated refactoring, comprehensive grep search |
| Cascade delete performance | Medium | Add indexes, batch delete operations |
| SubTask dependency validation complexity | Medium | Comprehensive unit tests, validation rules |
| UI performance with many ActionItems | Medium | Pagination, virtual scrolling |
| Breaking change for API consumers | High | Deprecation warning period, migration guide |

---

## 13. Success Criteria

✅ All unit tests passing (>90% coverage)  
✅ ActionItems CRUD operations working  
✅ Rollup calculations accurate  
✅ File attachments drag-and-drop functional  
✅ Unsaved changes modal working  
✅ Keyboard shortcuts operational  
✅ MiscTask → FollowupTask rename complete  
✅ No breaking changes for external APIs  
✅ Performance: <500ms for queries with 1000+ items  
✅ Database migration clean (no data loss)  

---

## 14. Appendix: Related Documents

- [TaskCreationDialog.requirements.md](./TaskCreationDialog.requirements.md)
- [SprintPlanningPage.requirements.md](./SprintPlanningPage.requirements.md)
- Code Analysis Screenshots (provided)

---

**Document Owner**: Development Team  
**Status**: Ready for Implementation  
**Approval Required**: Yes (Architectural Review)
