namespace Borderize;

public static class InputResolver
{
    static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static IEnumerable<string> Resolve(string input, bool recursive, string suffix)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        IEnumerable<string> files;

        if (Directory.Exists(input))
        {
            files = SupportedExtensions.SelectMany(ext =>
                Directory.EnumerateFiles(input, $"*{ext}", searchOption));
        }
        else if (input.Contains('*') || input.Contains('?'))
        {
            var dir = Path.GetDirectoryName(input);
            var pattern = Path.GetFileName(input);
            var baseDir = string.IsNullOrEmpty(dir) ? "." : dir;
            files = Directory.EnumerateFiles(baseDir, pattern, searchOption).Where(IsSupported);
        }
        else
        {
            // Single named file: unlike directory/glob mode the user asked for this
            // specific file, so surface a clear error rather than silently dropping it.
            if (!File.Exists(input))
                throw new FileNotFoundException($"File not found: {input}", input);
            if (!IsSupported(input))
                throw new ArgumentException(
                    $"Unsupported file type '{Path.GetExtension(input)}'. Supported: {string.Join(", ", SupportedExtensions)}");
            files = [input];
        }

        return files.Where(f => !IsAlreadyBordered(f, suffix));
    }

    static bool IsSupported(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    static bool IsAlreadyBordered(string path, string suffix)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        return nameWithoutExt.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
