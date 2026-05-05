using FluentAssertions;
using Xunit;

namespace Engine.Tests.Pipelines;

/// <summary>
/// Pure-managed tests for <see cref="MaterialPipelineRegistry"/> and
/// <see cref="PrepareMaterialPipelines"/>. The prepare system is exercised against
/// hand-built render-world entities; the MaterialX codegen path is gated on the
/// native runtime and excluded from the unit suite by setting
/// <see cref="MaterialDescription.MaterialXSource"/> to <c>null</c> so the entry
/// latches to <see cref="MaterialPipelineStatus.NoMaterialX"/>.
/// </summary>
[Trait("Category", "Unit")]
public class MaterialPipelineRegistryTests
{
    [Fact]
    public void Registry_TryGet_Returns_Null_When_Empty()
    {
        var reg = new MaterialPipelineRegistry();
        reg.TryGet(7).Should().BeNull();
        reg.Count.Should().Be(0);
    }

    [Fact]
    public void Registry_Set_Then_TryGet_Returns_Same_Entry()
    {
        var reg = new MaterialPipelineRegistry();
        var entry = new MaterialPipelineEntry { MaterialId = 11, Status = MaterialPipelineStatus.NoMaterialX };
        reg.Set(entry);

        reg.TryGet(11).Should().BeSameAs(entry);
        reg.Count.Should().Be(1);
    }

    [Fact]
    public void Prepare_Marks_Material_Without_MaterialX_As_NoMaterialX()
    {
        var lib = new MaterialLibrary();
        var handle = lib.Create(new MaterialDescription { Name = "Plain", SourcePath = "/Plain" });
        var rw = BuildRenderWorldWith(handle);

        new PrepareMaterialPipelines().Run(rw, FakeRenderContext());

        var reg = rw.Get<MaterialPipelineRegistry>();
        var entry = reg.TryGet(handle.Id);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(MaterialPipelineStatus.NoMaterialX);
    }

    [Fact]
    public void Prepare_Skips_Entities_Without_Valid_Material_Handle()
    {
        var rw = BuildRenderWorldWith(default);

        new PrepareMaterialPipelines().Run(rw, FakeRenderContext());

        rw.TryGet<MaterialPipelineRegistry>()?.Count.Should().Be(0);
    }

    [Fact]
    public void Prepare_Is_Idempotent_Per_Handle()
    {
        var lib = new MaterialLibrary();
        var handle = lib.Create(new MaterialDescription { Name = "Plain", SourcePath = "/Plain" });
        var rw = BuildRenderWorldWith(handle);
        var prep = new PrepareMaterialPipelines();

        prep.Run(rw, FakeRenderContext());
        var first = rw.Get<MaterialPipelineRegistry>().TryGet(handle.Id);

        prep.Run(rw, FakeRenderContext());
        var second = rw.Get<MaterialPipelineRegistry>().TryGet(handle.Id);

        second.Should().BeSameAs(first, "second pass must not overwrite an already-cached entry");
    }

    private static RenderWorld BuildRenderWorldWith(MaterialHandle handle)
    {
        var rw = new RenderWorld();
        var entity = rw.Entities.Spawn();
        rw.Entities.Add(entity, new RenderMeshInstance
        {
            MainEntityId = entity,
            VertexCount = 3,
            Material = handle,
        });
        return rw;
    }

    private static RenderContext FakeRenderContext()
    {
        // PrepareMaterialPipelines does not touch RenderContext today; passing null
        // would compile but a real RenderContext is more honest about the contract.
        return null!; // intentionally not dereferenced by the system under test.
    }
}

