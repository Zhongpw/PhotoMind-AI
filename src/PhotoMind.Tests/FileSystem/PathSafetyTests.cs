using PhotoMind.Core.Exceptions;
using PhotoMind.Infrastructure.FileSystem;

namespace PhotoMind.Tests.FileSystem;

public class PathSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeFolderName_Empty_ReturnsOther(string? input)
    {
        Assert.Equal("其他", PathSafety.SanitizeFolderName(input));
    }

    [Fact]
    public void SanitizeFolderName_InvalidCharacters_ReplacedWithUnderscore()
    {
        // \ / : * ? " < > | 都是非法文件名字符
        Assert.Equal("自然_海洋", PathSafety.SanitizeFolderName("自然/海洋"));
        Assert.Equal("a_b_c", PathSafety.SanitizeFolderName("a:b*c"));
        Assert.Equal("a_b_c", PathSafety.SanitizeFolderName("a<b>c"));
    }

    [Fact]
    public void SanitizeFolderName_TrailingDotAndSpace_Trimmed()
    {
        Assert.Equal("photo", PathSafety.SanitizeFolderName("photo. "));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX", "_AUX")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("COM9", "_COM9")]
    [InlineData("LPT1", "_LPT1")]
    public void SanitizeFolderName_ReservedDeviceNames_Prefixed(string input, string expected)
    {
        Assert.Equal(expected, PathSafety.SanitizeFolderName(input));
    }

    [Fact]
    public void SanitizeFolderName_NormalName_Unchanged()
    {
        Assert.Equal("自然", PathSafety.SanitizeFolderName("自然"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateDestination_Empty_Throws(string? input)
    {
        Assert.Throws<PathValidationException>(() => PathSafety.ValidateDestination(input!));
    }

    [Fact]
    public void ValidateDestination_RelativePath_Throws()
    {
        Assert.Throws<PathValidationException>(() => PathSafety.ValidateDestination("photos\\out"));
    }

    [Fact]
    public void ValidateDestination_DriveRoot_Throws()
    {
        Assert.Throws<PathValidationException>(() => PathSafety.ValidateDestination("C:\\"));
    }

    [Theory]
    [InlineData("C:\\Windows")]
    [InlineData("C:\\Windows\\Photos")]
    [InlineData("C:\\Program Files\\X")]
    [InlineData("C:\\ProgramData\\X")]
    [InlineData("C:\\System Volume Information\\X")]
    public void ValidateDestination_ProtectedTopLevel_Throws(string input)
    {
        Assert.Throws<PathValidationException>(() => PathSafety.ValidateDestination(input));
    }

    [Fact]
    public void ValidateDestination_NormalPath_DoesNotThrow()
    {
        PathSafety.ValidateDestination("D:\\PhotoMind_Output\\自然\\海洋");
    }
}
