# AI/ML Document Parsing & JSON Analysis Feature - Implementation Plan

**Document Status**: Comprehensive architecture review from Opus model  
**Last Updated**: 2024  
**Complexity**: Very Large (Multiple phases, AI/ML integration, background jobs, document processing)  
**Priority**: High

---

## Executive Summary

This plan outlines the implementation of an **AI/ML Document Parsing & JSON Analysis Feature** that extends the existing Hybrid Batch Processing Framework to support:

- ✅ Word document parsing using DocumentFormat.OpenXml + MLNET
- ✅ Structured data extraction from mixed-format documents (tables, text, structured fields)
- ✅ JSON diff detection and analysis for comparing document versions
- ✅ Background processing via Worker pattern with structured JSON reports
- ✅ Confidence scoring and validation for extracted fields
- ✅ Real-world use case: TMG Technical Upgrade Handover Documents

---

## 1. Overview & Architecture Integration

### Feature Scope

**Extend the existing Hybrid Batch Processing Framework to support**:

1. **Word Document Parsing**
   - DocumentFormat.OpenXml for structure extraction
   - MLNET for pattern recognition and entity extraction
   - Support for mixed-format documents (tables, text, structured fields)

2. **JSON Diff Detection**
   - Compare and analyze JSON files
   - Track changes between document versions
   - Generate diff reports with statistics (additions, deletions, modifications)

3. **Background Processing**
   - Worker pattern picks up events
   - DocumentParsingJobEvent triggered on file upload
   - Asynchronous processing via DocumentParsingJobHandler

4. **Structured JSON Reports**
   - Parsed document structure in JSON
   - Extraction results with confidence scores
   - Validation errors and field mapping

### Key Architectural Decision

**100% Local Processing with MLNET**:
- ✅ No external LLM or service calls required
- ✅ All document parsing runs on Worker host
- ✅ OpenXML for document structure extraction
- ✅ MLNET for pattern recognition and entity extraction
- ✅ JsonDiffPatch.Net for JSON comparison
- ✅ Zero external dependencies for core functionality
- ✅ Privacy: Documents never leave your infrastructure
- ✅ Cost-effective: No per-call charges
- ✅ Performance: Optimized for .NET 10, runs directly on Worker host
- ✅ Offline capable: Works without internet connectivity

### Integration Points

**User workflow**:
```
User Uploads .docx
    ↓
FileImportBatchJobEvent
    ↓
Worker picks up event
    ↓
DocumentParsingJobHandler
    ↓
OpenXML Parse + MLNET Extract
    ↓
Save to DocumentParsingResultEntity
    ↓
Generate JSON Report
    ↓
UI displays results with confidence scores
```

---

## 2. Implementation Components

### 2.1 New NuGet Packages

Add to `algration.command_control.Jobs.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.ML" Version="3.4.0" />
  <PackageReference Include="Microsoft.ML.AutoML" Version="0.21.0" />
  <PackageReference Include="Microsoft.ML.TensorFlow" Version="3.4.0" Optional="true" />
  <PackageReference Include="JsonDiffPatch.Net" Version="2.4.0" />
</ItemGroup>
```

**Note**: `DocumentFormat.OpenXml` already exists in the Jobs project

### 2.2 New Domain Entities

**A. DocumentParsingConfigEntity** (`Core/Domain/DocumentParsing/`)

Stores MLNET model configuration and parsing templates:
```csharp
public class DocumentParsingConfigEntity : TenantAuditableEntity
{
    public Guid TenantId { get; set; }
    public string DocumentType { get; set; } // e.g., "ContractForm", "InvoiceDoc"
    public string ModelLocation { get; set; } // Trained MLNET model location in blob storage
    public ParsedRules ParsingRules { get; set; } // JSON structure defining extraction rules
    
    // Implements: TenantAuditableEntity, IProjectScopedEntity
}
```

**B. DocumentParsingResultEntity** (`Core/Domain/DocumentParsing/`)

Stores parsing results with extracted data:
```csharp
public class DocumentParsingResultEntity : TenantAuditableEntity
{
    public Guid ImportFileId { get; set; } // Links to ImportFileEntity
    public string DocumentType { get; set; } // Parsed document classification
    public ExtractedDataJson ExtractedData { get; set; } // Structured JSON output
    public ConfidenceScores ConfidenceScores { get; set; } // MLNET confidence (0.0-1.0)
    public ParsingErrors ParsingErrors { get; set; } // Validation errors
    
    // Implements: TenantAuditableEntity, IProjectScopedEntity
}
```

**C. JsonComparerLsonResultEntity** (`Core/Domain/DocumentParsing/`)

Stores JSON diff analysis results:
```csharp
public class JsonComparerLsonResultEntity : TenantAuditableEntity
{
    public Guid SourceFileId { get; set; } // Links to ImportFileEntity
    public Guid TargetFileId { get; set; } // Links to ImportFileEntity
    public JsonDiffResultJson DiffResult { get; set; } // JsonDiffPatch output
    public SummaryMetrics Statistics { get; set; } // Additions, deletions, modifications
    
    // Implements: TenantAuditableEntity, IProjectScopedEntity
}
```

### 2.3 EF Core Configuration

Create configuration entities for persistence:

**DocumentParsingConfigEntityConfiguration**:
```csharp
builder.ToTable("DocumentParsingConfigs", schema: "core");
builder.HasKey(x => new { x.Id, x.TenantId });
builder.Property(x => x.ParsingRules).HasMaxLength(5000); // JSON
builder.HasIndex(x => new { x.TenantId, x.DocumentType });
```

**DocumentParsingResultEntityConfiguration**:
```csharp
builder.ToTable("DocumentParsingResults", schema: "core");
builder.HasKey(x => new { x.Id, x.TenantId });
builder.Property(x => x.ExtractedData).HasMaxLength(5000); // JSON
builder.HasIndex(x => new { x.TenantId, x.ImportFileId });
```

**JsonComparerLsonResultEntityConfiguration**:
```csharp
builder.ToTable("JsonComparerLsonResults", schema: "core");
builder.HasKey(x => new { x.Id, x.TenantId });
builder.Property(x => x.DiffResult).HasMaxLength(5000);
builder.HasIndex(x => new { x.TenantId, x.SourceFileId, x.TargetFileId });
```

### 2.4 Database Migration

**Generate migration**:
```powershell
dotnet ef migrations add AddDocumentParsingEntities `
  --startup-project src/algration.command_control.AppHost `
  --project src/algration.command_control.Persistence
```

**Migration will**:
1. Create `core.DocumentParsingConfigs` table
2. Create `core.DocumentParsingResults` table
3. Create `core.JsonComparerLsonResults` table
4. Create indexes on (TenantId, DocumentType) and (ImportFileId)

---

## 3. Service Layer Implementation

### 3.1 DocumentParsingService Contract

**File**: `Core/Contracts/IDocumentParsingService.cs`

```csharp
public interface IDocumentParsingService
{
    // Document Parsing
    Task<DocumentParsingResultDto> ParseDocumentAsync(
        Guid importFileId, CancellationToken ct);
    
    Task<DocumentParsingResultDto> ParseDocumentWithModelAsync(
        Guid importFileId, Guid modelId, CancellationToken ct);
    
    // Model Training
    Task<DocumentParsingConfigDto> TrainModelAsync(
        DocumentParsingTrainingDataSet trainingData, CancellationToken ct);
    
    Task SaveTrainedModelAsync(Guid modelId, byte[] modelBytes);
    
    // Configuration
    Task<DocumentParsingConfigDto> GetConfigAsync(string documentType);
    Task UpdateConfigAsync(Guid configId, DocumentParsingConfigDto dto);
    
    // Results Query
    Task<IEnumerable<DocumentParsingResultDto>> GetParsingResultsAsync(
        Guid importFileId);
    Task<DocumentParsingResultDto> GetParsingResultAsync(Guid resultId);
}
```

### 3.2 DocumentParsingService Implementation

**File**: `Jobs/Services/DocumentParsingService.cs`

**Core Parsing Logic**:

```csharp
public async Task<DocumentParsingResultDto> ParseDocumentAsync(
    Guid importFileId, CancellationToken ct)
{
    // 1. Download encrypted blob → decrypt stream
    var documentBlob = await _blobStorage.DownloadAsync(importFileId, ct);
    var decryptedStream = await _encryptionService.DecryptAsync(documentBlob);
    
    // 2. OpenXML document traversal (tables, paragraphs)
    using (var wordDoc = WordprocessingDocument.Open(decryptedStream, false))
    {
        var mainPart = wordDoc.MainDocumentPart;
        var document = mainPart.Document;
        
        // Extract paragraphs and tables
        var paragraphs = document.Descendants<Paragraph>();
        var tables = document.Descendants<Table>();
        
        // 3. MLNET prediction pipeline
        var extractedData = await RunMLNETExtractionAsync(paragraphs, tables, ct);
        
        // 4. Confidence thresholding & validation
        ValidateExtractionQuality(extractedData);
        
        // 5. Serialize to DocumentParsingResultEntity
        var result = new DocumentParsingResultEntity 
        {
            ImportFileId = importFileId,
            ExtractedData = extractedData,
            ConfidenceScores = confidenceMetrics
        };
        
        await _context.DocumentParsingResults.AddAsync(result, ct);
        await _context.SaveChangesAsync(ct);
        
        return _mapper.Map<DocumentParsingResultDto>(result);
    }
}
```

**Key Implementation Details**:

1. **Download Encrypted Blob** → Decrypt stream
2. **OpenXML Document Traversal** (tables, paragraphs)
3. **MLNET Prediction Pipeline**
   - Confidence Thresholding & Validation
   - Serialize to ExtractedDataJson
4. **Serialize to DocumentParsingResultEntity**
5. **Stage extracted data in staging.StagedImports**
6. **Send notification to user**
7. **Optionally trigger downstream job (e.g., data validation)**

---

## 4. JSON Analysis Services

### 4.1 JsonComparerLsonService Contract

**File**: `Core/Contracts/IJsonComparerLsonService.cs`

```csharp
public interface IJsonComparerLsonService
{
    // JSON Diff Analysis
    Task<JsonComparerLsonResultDto> CompareJsonFilesAsync(
        Guid sourceFileId, Guid targetFileId, CancellationToken ct);
    
    // Diff Report Generation
    Task<DiffReportDto> GenerateDiffReportAsync(Guid resultId);
    
    // Validation
    Task<DiffValidationResultDto> ValidateDiffAsync(
        Guid sourceFileId, Guid targetFileId);
}
```

### 4.2 JsonComparerLsonService Implementation

**File**: `Jobs/Services/JsonComparerLsonService.cs`

**Core Comparison Logic**:

```csharp
public async Task<JsonComparerLsonResultDto> CompareJsonFilesAsync(
    Guid sourceFileId, Guid targetFileId, CancellationToken ct)
{
    // 1. Download source & target JSON files
    var sourceBlob = await _blobStorage.DownloadAsync(sourceFileId, ct);
    var targetBlob = await _blobStorage.DownloadAsync(targetFileId, ct);
    
    var sourceJson = await _encryptionService.DecryptToJsonAsync(sourceBlob);
    var targetJson = await _encryptionService.DecryptToJsonAsync(targetBlob);
    
    // 2. JsonDiffPatch comparison
    var jdp = new JsonDiffPatchCoreModule();
    var patchDocument = jdp.Diff(sourceJson, targetJson);
    
    // 3. Generate summary metrics (additions, deletions, modifications)
    var metrics = CalculateDiffMetrics(patchDocument);
    
    // 4. Serialize to JsonComparerLsonResultEntity
    var result = new JsonComparerLsonResultEntity 
    {
        SourceFileId = sourceFileId,
        TargetFileId = targetFileId,
        DiffResult = patchDocument,
        Statistics = metrics
    };
    
    await _context.JsonComparerLsonResults.AddAsync(result, ct);
    await _context.SaveChangesAsync(ct);
    
    return _mapper.Map<JsonComparerLsonResultDto>(result);
}

private DiffMetrics CalculateDiffMetrics(JToken patchDocument)
{
    var metrics = new DiffMetrics 
    {
        AddedCount = CountAdditions(patchDocument),
        DeletedCount = CountDeletions(patchDocument),
        ModifiedCount = CountModifications(patchDocument)
    };
    return metrics;
}
```

---

## 5. Background Job Integration

### 5.1 DocumentParsingJobEvent

**File**: `Core/Events/DocumentParsing/DocumentParsingJobEvent.cs`

```csharp
public record DocumentParsingJobEvent(
    Guid ImportFileId,
    Guid ProjectId,
    string DocumentType,
    Guid CreatedBy) : BaseJobEvent(ProjectId, CreatedBy);
```

### 5.2 DocumentParsingJobHandler

**File**: `Jobs/Listeners/DocumentParsingJobHandler.cs`

**Implements**: `IBatchJobHandler<DocumentParsingJobEvent>`

```csharp
public async Task ProcessAsync(DocumentParsingJobEvent jobEvent, CancellationToken ct)
{
    try 
    {
        // 1. Parse document
        var result = await _documentParsingService
            .ParseDocumentAsync(jobEvent.ImportFileId, ct);
        
        // 2. Validate extraction quality
        if (result.ConfidenceScores.Average < 0.75)
        {
            // Log warning but continue - flag for manual review
            await LogWarningAsync($"Low confidence scores for {jobEvent.ImportFileId}");
        }
        
        // 3. Send notification to user
        await _notificationService.SendParsingCompleteAsync(
            jobEvent.CreatedBy, result, ct);
        
        // 4. Optionally trigger downstream job
        // e.g., DataValidationJobEvent for structured validation
        
    }
    catch (Exception ex)
    {
        // Error handling with detailed logging
        await LogErrorAsync(ex, jobEvent);
        throw; // Batch framework will retry
    }
}
```

---

## 6. File Type Extensions

### Update ImportFileTypeEnum

**File**: `Shared/DataListTypes/TaskFileTypeEnum.cs`

```csharp
public enum ImportFileTypeEnum
{
    // Existing types...
    WordDocument = 10,  // .docx parsing
    JsonComparison = 11 // JSON diff analysis
}
```

---

## 7. Database Migrations

### Migration File Details

**Generated Migration**: `AddDocumentParsing_CoreEntities.cs`

**Creates**:
1. `core.DocumentParsingConfigs` - MLNET model configurations
2. `core.DocumentParsingResults` - Parsing outputs
3. `core.JsonComparerLsonResults` - Diff results

All tables include:
- Sequential UUID/D7 primary keys
- Tenant isolation (TenantId with indexes)
- Audit fields (CreatedDate, CreatedBy, UpdatedDate, UpdatedBy)
- Soft delete support (IsDeleted, DeletedAtUtc)
- Composite indexes for common queries

---

## 8. UI Integration (Optional for Phase 1)

### Minimal UI Additions (Phase 1 - Optional)

In `web/[Admin]/[Imports/]`:

1. **Add .docx file type option** in upload wizard
2. **Display parsing results** with confidence scores
3. **Show JSON diff reports** in read-only grid

**Defer full UI to Phase 2** - Focus on background processing pipeline first

---

## 9. MLNET Model Training & Management

### Training Pipeline (`Jobs/ML/Training/`)

**Create DocumentClassificationTrainer**:

```csharp
public async Task<ITransformer> TrainAsync(
    DocumentParsingTrainingDataSet trainingData, 
    CancellationToken ct)
{
    var mlContext = new MLContext();
    
    // 1. Load training data from labeled samples
    var dataView = mlContext.Data.LoadFromEnumerable(trainingData.LabeledDocuments);
    
    // 2. Build AutoML classification pipeline
    var pipeline = mlContext.AutoML.CreateTextClassificationEstimator(
        labelColumnName: "Label",
        textColumnName: "DocumentContent"
    );
    
    // 3. Train model and save to blob storage
    var model = pipeline.Fit(dataView);
    var modelBytes = SerializeModel(model);
    
    await _blobStorage.UploadAsync(modelId, modelBytes, ct);
    
    return model;
}
```

### Model Storage

Store trained models in Azure Blob Storage:
```
tenants/{tenantId}/ml-models/document-parsing/{version}/model.zip
```

### Model Versioning

Track models per tenant with version history:
```csharp
public class DocumentParsingConfigEntity
{
    public string ModelLocation { get; set; }
    public int ModelVersion { get; set; }
    public DateTime TrainedDate { get; set; }
    public decimal AccuracyScore { get; set; }
}
```

---

## 10. Configuration & Settings

### appsettings.json

```json
{
  "DocumentParsing": {
    "EnableParsing": true,
    "MinConfidenceThreshold": 0.75,
    "MaxDocumentSizeMB": 50,
    "DefaultModelVersion": "v1.0"
  },
  "JsonComparison": {
    "EnableDiffAnalysis": true,
    "MaxComparisonSizeMB": 100
  }
}
```

---

## 11. Real-World Use Case: TMG Technical Upgrade Handover Documents

### Document Samples Analyzed

Three TMG_TU_Handover document types:
- **PC.docx** (Policy Center) - 27.5 KB
- **CM.docx** (ContactManager) - 7.7 KB
- **CC.docx** (Claim Center) - 10.9 KB

### Document Structure

**Consistent template with**:
- Header: Customer, Source/Target versions, Project Manager
- 10+ Sections: Out of Scope, OOTB Changes, Known Issues, User Stories, TODOs, Admin Data, Deliverables, etc.
- Structured Tables: Description/Comment columns throughout

### Extraction Goals

1. **Parse all three document types** into structured JSON
2. **Extract metadata**: Customer, versions, manager
3. **Parse section tables** into arrays of {description, comment} objects
4. **Compare PC vs CM vs CC** to identify differences
5. **Track changes** between document versions over time

### MLNET Training Approach

**Classification Model** (section headers):
- Train on section headers from sample documents
- Classify paragraphs as belonging to specific sections
- Entity extraction for metadata fields

**Confidence Scoring**:
- Per extracted field (customer, version, manager)
- Per section (Out of Scope, OOTB Changes, etc.)
- Per table row

---

## 12. Implementation Phases

### Phase 1: Core Infrastructure (1-2 weeks)
- [ ] Create domain entities (DocumentParsingConfig, DocumentParsingResult, JsonComparerLson)
- [ ] Create EF Core configurations & migration
- [ ] Implement DocumentParsingService (core logic)
- [ ] Implement JsonComparerLsonService (diff analysis)
- [ ] Create DocumentParsingJobEvent and handler
- [ ] Add unit tests for parsing logic
- [ ] No UI - background processing pipeline only

### Phase 2: MLNET Services (1-2 weeks)
- [ ] Implement DocumentClassificationTrainer
- [ ] Create initial MLNET model (section headers)
- [ ] Train confidence scoring pipeline
- [ ] Implement model versioning & storage
- [ ] Add training pipeline tests
- [ ] Real-world test with TMG documents

### Phase 3: JSON Analysis (1 week)
- [ ] Implement JsonDiffPatch integration
- [ ] Generate diff reports with statistics
- [ ] Support nested object comparison
- [ ] Handle large JSON files efficiently
- [ ] Add diff analysis tests

### Phase 4: Background Jobs (1 week)
- [ ] Register DocumentParsingJobEvent listener
- [ ] Implement job event handling
- [ ] Error handling & retry logic
- [ ] Notification integration
- [ ] End-to-end workflow testing

### Phase 5: Integration & Testing (1-2 weeks)
- [ ] Extend ImportFileService to trigger document parsing events
- [ ] Add integration tests (document upload → parsing → results)
- [ ] Test multi-tenant isolation
- [ ] Blob storage encryption/decryption integration
- [ ] Performance testing with large documents

### Phase 6: UI Integration (1 week - Optional)
- [ ] Add .docx file type option in upload wizard
- [ ] Display parsing results with confidence scores
- [ ] Show JSON diff reports in read-only grid
- [ ] Add JSON comparison UI
- [ ] Defer full UI to Phase 2 - Focus on background processing pipeline first

**Total Estimated**: 6-9 weeks

---

## 13. Testing Strategy

### Unit Tests

**DocumentParsingService Tests**:
- MLNET parsing logic
- JSON extraction with confidence scores
- Entity validators
- Error handling

**JsonComparerLsonService Tests**:
- JSON diff algorithm
- Statistics calculation (additions, deletions, modifications)
- Nested object comparison
- Large JSON file handling

**Background Job Tests**:
- DocumentParsingJobEvent handling
- Error retry logic
- Notification dispatch

### Integration Tests

**End-to-End Document Upload → Parsing → Results**:
- Multi-tenant isolation verification
- Blob storage encryption/decryption
- Database persistence
- Background job processing
- Result accessibility via API

**Performance Tests**:
- Large document parsing (50MB+)
- Batch processing throughput
- MLNET model inference speed
- JSON diff with large files

---

## 14. Dependencies & Risks

### External Dependencies

**MLNET Framework Stability**:
- ⚠️ MLNET (Microsoft-supported) - Mature, well-maintained OSS
- ⚠️ OpenXML format compatibility - Industry standard
- ⚠️ JsonDiffPatch.Net library - Well-maintained OSS

### Risks & Mitigations

| Risk | Impact | Mitigation |
|:---|:---:|---|
| **ML Model Accuracy** | Medium | Mitigate with confidence thresholds and manual review flags |
| **Document Format Variations** | Medium | Document format validations; Use AutoML for adaptability |
| **Performance: Large Files (50MB+)** | Medium | Implement streaming for large files, consider model caching |
| **MLNET Model Size** | Low | Models typically 5-50MB; Store in blob storage |

---

## 15. Security Considerations

### Zero-Trust Multi-Tenancy

- ✅ **All operations tenant-scoped** via TenantId
- ✅ **Encryption at Rest**: Documents encrypted in blob storage
- ✅ **Encryption in Transit**: TLS for all API calls
- ✅ **ML Model Isolation**: Per-tenant model storage

### Encryption at Rest

- Documents encrypted in blob storage
- Encryption keys managed via Azure Key Vault
- Decryption happens only in-memory on Worker

### Audit Trail

- All operations logged to `audit.AuditRecords`
- Track document uploads, parsing results, comparisons
- User attribution via CreatedBy, ModifiedBy

---

## 16. Success Metrics

✅ **Parsing Accuracy**:
- [ ] Parse 95%+ of standard Word documents successfully
- [ ] Confidence scores > 0.75 for key fields

✅ **Performance**:
- [ ] Parse 5-10MB document in <5 seconds
- [ ] JSON diff 100MB file in <2 seconds
- [ ] <500ms for confidence threshold & validation

✅ **JSON Analysis**:
- [ ] Correctly identify additions, deletions, modifications
- [ ] Confidence scores for diff accuracy

✅ **Real-World TMG Use Case**:
- [ ] Parse all 3 TMG document types
- [ ] Extract metadata (customer, versions, manager)
- [ ] Compare documents with >90% accuracy
- [ ] Track changes over time

---

## 17. Architecture Diagram

```
User Uploads .docx
    ↓
FileImportBatchJobEvent
    ↓
DocumentParsingJobHandler (Worker)
    ↓
+─────────────────────────────────────────+
│ 1. Download encrypted blob              │
│ 2. OpenXML parse (paragraphs, tables)   │
│ 3. MLNET extraction pipeline            │
│ 4. Confidence scoring & validation      │
│ 5. Save DocumentParsingResultEntity     │
│ 6. Stage extracted data                 │
│ 7. Send notification to user            │
│ 8. Optionally trigger downstream job    │
+─────────────────────────────────────────+
    ↓
DocumentParsingResultEntity (Database)
    ↓
API returns results with confidence scores
    ↓
UI displays parsed data (Phase 6)
```

---

## 18. Key Files to Create/Modify

### New Files (~25)

**Domain Entities** (3):
- DocumentParsingConfigEntity.cs
- DocumentParsingResultEntity.cs
- JsonComparerLsonResultEntity.cs

**Entity Validators** (2):
- DocumentParsingConfigValidator.cs
- JsonComparerLsonValidator.cs

**EF Core Configurations** (3):
- DocumentParsingConfigEntityConfiguration.cs
- DocumentParsingResultEntityConfiguration.cs
- JsonComparerLsonResultEntityConfiguration.cs

**Service Interfaces** (2):
- IDocumentParsingService.cs
- IJsonComparerLsonService.cs

**Service Implementations** (2):
- DocumentParsingService.cs
- JsonComparerLsonService.cs

**Background Jobs** (2):
- DocumentParsingJobEvent.cs
- DocumentParsingJobHandler.cs

**Job Events** (1):
- DocumentParsingJobEvent.cs

**DTOs** (6):
- DocumentParsingConfigDto.cs
- DocumentParsingResultDto.cs
- JsonComparerLsonResultDto.cs
- DiffReportDto.cs
- DiffValidationResultDto.cs
- DocumentParsingTrainingDataSet.cs

**MLNET Services** (2):
- DocumentClassificationTrainer.cs
- ModelStorageService.cs

**Migration** (1) - Auto-generated:
- AddDocumentParsing_CoreEntities.cs

**Unit Tests** (4):
- DocumentParsingServiceTests.cs
- JsonComparerLsonServiceTests.cs
- DocumentParsingJobHandlerTests.cs
- DocumentClassificationTrainerTests.cs

### Modified Files (~9)

- `AppDbContext.cs` - Register new DbSets
- `TaskFileTypeEnum.cs` - Add WordDocument, JsonComparison types
- `ImportFileService.cs` - Trigger DocumentParsingJobEvent
- `Jobs.csproj` - Add NuGet packages
- `appsettings.json` - Add configuration
- `JobsExtensions.cs` - Register new handlers
- `IAppSettings.cs` - Configuration interfaces
- `MigrationExtensions.cs` - Register migration
- `Migration file (schema changes)`

---

## 19. Next Steps

### Approval & Kickoff

1. **Architecture Review**: Confirm MLNET + OpenXML approach
2. **Real-World Validation**: Test with actual TMG documents
3. **Performance Baseline**: Benchmark with 50MB+ documents
4. **Team Allocation**: Assign AI/ML engineer + backend engineer

### Phase 1 Kickoff

- [ ] Create feature branch: `feature/document-parsing`
- [ ] Set up local MLNET development environment
- [ ] Begin domain entity & service implementation
- [ ] Establish test fixtures with real documents

---

## Appendix: References

- **OpenXML Documentation**: https://learn.microsoft.com/en-us/office/open-xml/
- **MLNET Documentation**: https://learn.microsoft.com/en-us/dotnet/machine-learning/
- **JsonDiffPatch.Net**: https://github.com/wbond/json-diff-patch-net
- **Real-World Use Case**: TMG Technical Upgrade Handover (3 documents, ~45KB total)

---

**Document Owner**: Development Team  
**Status**: Ready for Architecture Review  
**Approval Required**: Yes  
**Timeline**: 6-9 weeks implementation  
**Last Updated**: 2024
