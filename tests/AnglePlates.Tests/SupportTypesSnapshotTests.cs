using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Core;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests;

// The one reviewed snapshot of the support types, for each base access modifier. They don't depend on any template.
public sealed class SupportTypesSnapshotTests
{
    [Test]
    [Arguments("internal")]
    [Arguments("public")]
    public Task Snapshot_BaseAccessModifier_MatchesReviewedSources(string accessModifier)
    {
        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithAccessModifier(accessModifier));

        return GeneratorSnapshot.Verify(result.RunResult, $"SupportTypes.{accessModifier}", _ => true);
    }
}
