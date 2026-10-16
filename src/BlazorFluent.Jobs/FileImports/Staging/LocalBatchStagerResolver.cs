using BlazorFluent.Shared.Contracts.Engine.Transforms;

namespace BlazorFluent.Jobs.FileImports.Staging;

/// <summary>
/// Pre-ingestion router that resolves the appropriate ICanonicalBatchStager based on FileTemplateConfig.StagingBatchStrategy.
/// Routes 80% standard files ("generic-json") to GenericTabularJsonBatchStager and 20% complex files ("custom:...") to custom stagers.
/// </summary>
public interface ILocalBatchStagerResolver
{
    ICanonicalBatchStager ResolveStager(FileTemplateConfig config);
}

public sealed class LocalBatchStagerResolver : ILocalBatchStagerResolver
{
    private readonly IEnumerable<ICanonicalBatchStager> _stagers;

    public LocalBatchStagerResolver(IEnumerable<ICanonicalBatchStager> stagers)
    {
        _stagers = stagers;
    }

    public ICanonicalBatchStager ResolveStager(FileTemplateConfig config)
    {
        var requestedStrategy = string.IsNullOrWhiteSpace(config.StagingBatchStrategy)
            ? "generic-json"
            : config.StagingBatchStrategy.Trim();

        // 1. Direct match on StrategyName (e.g., "generic-json" or "custom:VendorInvoiceXmlBatchStager")
        var matched = _stagers.FirstOrDefault(s =>
            string.Equals(s.StrategyName, requestedStrategy, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
            return matched;

        // 2. Match by class name if prefixed with "custom:"
        if (requestedStrategy.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            var className = requestedStrategy["custom:".Length..].Trim();
            matched = _stagers.FirstOrDefault(s =>
                string.Equals(s.GetType().Name, className, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.GetType().FullName, className, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
                return matched;

            throw new InvalidOperationException(
                $"Custom staging batch strategy '{requestedStrategy}' declared for fileType '{config.FileType}' is not registered in DI.");
        }

        // 3. Default to generic-json
        return _stagers.First(s => string.Equals(s.StrategyName, "generic-json", StringComparison.OrdinalIgnoreCase));
    }
}
