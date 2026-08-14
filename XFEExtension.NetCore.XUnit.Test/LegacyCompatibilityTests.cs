using XFEExtension.NetCore.XUnit.Assertions;
using XFEExtension.NetCore.XUnit.Attributes;

namespace XFEExtension.NetCore.XUnit.Test;

#pragma warning disable CS0618, XFE0100
[CTest]
internal sealed class LegacyCompatibilityTests
{
    private bool _setUp;

    [SetUp]
    public void SetUp() => _setUp = true;

    [MTest]
    public void RunsLegacyTest() => Assert.True(_setUp);

    [MRTest(1, 2, 3)]
    public int ComparesLegacyReturnValue(int left, int right) => left + right;

    [SMTest]
    public int RunsLegacyBenchmark() => 40 + 2;
}
#pragma warning restore CS0618, XFE0100
