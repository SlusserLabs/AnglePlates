using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// TemplatePath names a template relative to the project folder with '/' separators, or falls back to the full path
public sealed class TemplatePathTests
{
    [Test]
    [Arguments("C:\\work\\App\\Templates\\a.html", "C:\\work\\App\\", "Templates/a.html")]
    [Arguments("C:\\Work\\App\\Templates\\A.html", "c:\\work\\app\\", "Templates/A.html")]
    [Arguments("C:\\work\\Shared\\receipt.html", "C:\\work\\App\\", "../Shared/receipt.html")]
    [Arguments("D:\\elsewhere\\Shared\\receipt.html", "D:\\elsewhere\\App", "../Shared/receipt.html")]
    [Arguments("C:\\Work\\Shared\\receipt.html", "c:\\work\\App\\Sub\\", "../../Shared/receipt.html")]
    [Arguments("C:\\Templates\\a.html", "C:\\", "Templates/a.html")]
    [Arguments("C:/work/App/Templates/a.html", "C:/work/App", "Templates/a.html")]
    [Arguments("\\\\server\\share\\App\\Templates\\a.html", "\\\\SERVER\\share\\App\\", "Templates/a.html")]
    [Arguments("\\\\server\\share\\Shared\\receipt.html", "\\\\server\\share\\App\\", "../Shared/receipt.html")]
    [Arguments("\\\\server\\share\\a.html", "\\\\server\\share", "a.html")]
    [Arguments("/home/me/App/Templates/a.html", "/home/me/App/", "Templates/a.html")]
    [Arguments("/srv/build/Shared/receipt.html", "/srv/build/App", "../Shared/receipt.html")]
    [Arguments("/Repo/App/Templates/a.html", "/repo/app/", "../../Repo/App/Templates/a.html")]
    [Arguments("/Templates/a.html", "/", "Templates/a.html")]
    // A path that is the project folder itself still keeps its last segment
    [Arguments("/home/me/App", "/home/me/App", "../App")]
    // Paths with no relative form fall back to the full path
    [Arguments("D:\\Templates\\a.html", "C:\\App\\", "D:/Templates/a.html")]
    [Arguments("/Templates/a.html", "C:\\App\\", "/Templates/a.html")]
    [Arguments("C:\\Templates\\a.html", "/App/", "C:/Templates/a.html")]
    [Arguments("\\\\server\\other\\Templates\\a.html", "\\\\server\\share\\App\\", "//server/other/Templates/a.html")]
    [Arguments("C:\\Templates\\a.html", "", "C:/Templates/a.html")]
    [Arguments("Templates\\a.html", "", "Templates/a.html")]
    public async Task GetRelativePath_PathAndProjectDir_ReturnsRelativeOrFullPath(string path, string projectDir, string expected)
    {
        var relativePath = TemplatePath.GetRelativePath(path, projectDir);

        await Assert.That(relativePath).IsEqualTo(expected);
    }
}
