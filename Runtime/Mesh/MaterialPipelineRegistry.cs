using System.Collections.Generic;

namespace Engine;

/// <summary>
/// Status of a <see cref="MaterialPipelineEntry"/> in the
/// <see cref="MaterialPipelineRegistry"/>.
/// </summary>
public enum MaterialPipelineStatus
{
    /// <summary>The entry is queued but has not been processed yet.</summary>
    Pending,

    /// <summary>SPIR-V is compiled and reflection bindings are recovered; ready for downstream pipeline creation.</summary>
    Ready,

    /// <summary>Codegen, SPIR-V compile, or pipeline creation failed; the renderer must use the fallback pipeline.</summary>
    Failed,

    /// <summary>The source <see cref="MaterialDescription"/> has no MaterialX network; fallback pipeline applies.</summary>
    NoMaterialX,
}

/// <summary>
/// Per-material codegen result owned by <see cref="MaterialPipelineRegistry"/>.
/// Carries the generated GLSL, the SPIR-V bytecode, and the recovered descriptor
/// bindings so a downstream slice can build the matching <see cref="IPipeline"/>
/// and per-material <see cref="IDescriptorSet"/> without re-running codegen.
/// </summary>
public sealed class MaterialPipelineEntry
{
    /// <summary>Material handle id this entry was generated for.</summary>
    public required int MaterialId { get; init; }

    /// <summary>Current status of the entry.</summary>
    public MaterialPipelineStatus Status { get; set; }

    /// <summary>Generated GLSL pair when <see cref="Status"/> is <see cref="MaterialPipelineStatus.Ready"/>; otherwise <c>null</c>.</summary>
    public MaterialXGeneratedShader? Generated { get; set; }

    /// <summary>Compiled SPIR-V bytecode when <see cref="Status"/> is <see cref="MaterialPipelineStatus.Ready"/>; otherwise <c>null</c>.</summary>
    public MaterialXSpirvShader? Spirv { get; set; }

    /// <summary>Reason text when <see cref="Status"/> is <see cref="MaterialPipelineStatus.Failed"/>.</summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// Render-thread cache of MaterialX → SPIR-V results, keyed by
/// <see cref="MaterialHandle.Id"/>. <see cref="PrepareMaterialPipelines"/> populates
/// it once per unique material; downstream draw functions consult it to choose
/// between the per-material pipeline and the shared static-white fallback.
/// </summary>
/// <remarks>
/// The registry intentionally stops short of creating the Vulkan
/// <see cref="IPipeline"/> / <see cref="IDescriptorSet"/>: that step needs a render
/// pass + descriptor-set layout reflected from the MaterialX-generated GLSL and is
/// the focus of the next slice. Storing the SPIR-V + bindings here keeps that
/// future work an additive change and avoids re-running codegen every frame.
/// </remarks>
public sealed class MaterialPipelineRegistry
{
    private readonly Dictionary<int, MaterialPipelineEntry> _byId = new();

    /// <summary>Number of cached entries (any status).</summary>
    public int Count => _byId.Count;

    /// <summary>Looks up the cached entry for <paramref name="materialId"/>; returns <c>null</c> when not yet processed.</summary>
    public MaterialPipelineEntry? TryGet(int materialId)
        => _byId.TryGetValue(materialId, out var e) ? e : null;

    /// <summary>Inserts or replaces the entry for its <see cref="MaterialPipelineEntry.MaterialId"/>.</summary>
    public void Set(MaterialPipelineEntry entry)
        => _byId[entry.MaterialId] = entry;

    /// <summary>Snapshot of every entry currently cached (allocation-free enumeration).</summary>
    public IEnumerable<MaterialPipelineEntry> Entries => _byId.Values;

    /// <summary>Drops every entry; SPIR-V buffers are eligible for GC after the next collection.</summary>
    public void Clear() => _byId.Clear();
}

