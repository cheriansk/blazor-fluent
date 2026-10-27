using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace BlazorEnterprise.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<KeysetPaginationBenchmark>();
    }
}

public class BenchmarkRecord
{
    public long Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

[MemoryDiagnoser]
public class KeysetPaginationBenchmark
{
    private List<BenchmarkRecord> _dataset = new();

    [Params(10_000, 50_000)]
    public int TotalRecords;

    [GlobalSetup]
    public void Setup()
    {
        _dataset = Enumerable.Range(1, TotalRecords)
            .Select(i => new BenchmarkRecord { Id = i, Data = $"Payload-{i}" })
            .ToList();
    }

    [Benchmark(Baseline = true)]
    public List<BenchmarkRecord> OffsetPagination()
    {
        // Simulates typical deep pagination offset query: .Skip(N).Take(15)
        return _dataset.Skip(TotalRecords - 100).Take(15).ToList();
    }

    [Benchmark]
    public List<BenchmarkRecord> KeysetPagination()
    {
        // Simulates O(1) indexed Keyset pagination: .Where(x => x.Id > cursor).Take(15)
        var cursor = (long)(TotalRecords - 100);
        return _dataset.Where(x => x.Id > cursor).Take(15).ToList();
    }
}
