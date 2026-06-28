using Borderize;
using Xunit;

namespace Borderize.Tests;

public class InputResolverTests : IDisposable
{
    private readonly string _tempDir;

    public InputResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void DirectoryMode_ReturnsSupportedExtensions_AndExcludesUnsupported()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.jpg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.png"), "");
        File.WriteAllText(Path.Combine(_tempDir, "c.webp"), "");
        File.WriteAllText(Path.Combine(_tempDir, "d.gif"), "");
        File.WriteAllText(Path.Combine(_tempDir, "e.bmp"), "");

        var result = InputResolver.Resolve(_tempDir, recursive: false, suffix: "-border").ToList();

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, f => f.EndsWith(".gif"));
        Assert.DoesNotContain(result, f => f.EndsWith(".bmp"));
    }

    [Fact]
    public void DirectoryMode_SkipsFilesWhoseNameAlreadyEndsWithSuffix()
    {
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo-border.jpg"), "");

        var result = InputResolver.Resolve(_tempDir, recursive: false, suffix: "-border").ToList();

        Assert.Single(result);
        Assert.Contains(result, f => Path.GetFileName(f) == "photo.jpg");
    }

    [Fact]
    public void DirectoryMode_Recursive_FindsFilesInSubdirectories()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "top.jpg"), "");
        File.WriteAllText(Path.Combine(sub.FullName, "nested.png"), "");

        var result = InputResolver.Resolve(_tempDir, recursive: true, suffix: "-border").ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GlobMode_MatchesPatternAndFiltersUnsupportedExtensions()
    {
        File.WriteAllText(Path.Combine(_tempDir, "cat.jpg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "dog.jpg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "readme.gif"), "");

        var result = InputResolver.Resolve(Path.Combine(_tempDir, "*.jpg"), recursive: false, suffix: "-border").ToList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, f => f.EndsWith(".gif"));
    }

    [Fact]
    public void SingleFile_PassesThrough_WhenNotAlreadyBordered()
    {
        var path = Path.Combine(_tempDir, "photo.jpg");
        File.WriteAllText(path, "");

        var result = InputResolver.Resolve(path, recursive: false, suffix: "-border").ToList();

        Assert.Single(result);
        Assert.Equal(path, result[0]);
    }

    [Fact]
    public void SingleFile_ReturnsEmpty_WhenAlreadyBordered()
    {
        var path = Path.Combine(_tempDir, "photo-border.jpg");
        File.WriteAllText(path, "");

        var result = InputResolver.Resolve(path, recursive: false, suffix: "-border").ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void SingleFile_Throws_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_tempDir, "missing.jpg");

        Assert.Throws<FileNotFoundException>(
            () => InputResolver.Resolve(path, recursive: false, suffix: "-border").ToList());
    }

    [Fact]
    public void SingleFile_Throws_WhenExtensionUnsupported()
    {
        var path = Path.Combine(_tempDir, "notes.txt");
        File.WriteAllText(path, "");

        Assert.Throws<ArgumentException>(
            () => InputResolver.Resolve(path, recursive: false, suffix: "-border").ToList());
    }
}
