#if UNITY_EDITOR
using System;
using Unity.Profiling;

public static class KarmaAllocationChecks
{
    // Unity's Mono host can return zero from the .NET thread-allocation counter
    // even for allocating code. Record actual GC.Alloc events without frame aggregation.
    public static long Count(Action operation)
    {
        // These probes are deliberately repeatable; warm the delegate/JIT and
        // the whole workload before observing its steady-state allocations.
        operation();
        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            if (!recorder.Valid) throw new InvalidOperationException("GC.Alloc recorder is unavailable.");
            operation();
            recorder.Stop();
            if (recorder.Count == recorder.Capacity)
                throw new InvalidOperationException("Allocation measurement exceeded its sample capacity.");
            return recorder.Count;
        }
    }
}
#endif
