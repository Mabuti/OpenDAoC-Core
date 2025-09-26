using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace DOL.GS;

internal static class DetourLibraryInstaller
{
    private static readonly object _lock = new();
    private static bool _initialized;

    public static void EnsureNativeLibraryPresent()
    {
        if (_initialized)
            return;

        lock (_lock)
        {
            if (_initialized)
                return;

            string baseDirectory = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
            string targetDirectory = Path.Combine(baseDirectory, "lib");
            Directory.CreateDirectory(targetDirectory);

            string targetPath = Path.Combine(targetDirectory, "Detour.so");

            if (!File.Exists(targetPath))
                ExtractLibrary(targetPath);

            _initialized = true;
        }
    }

    private static void ExtractLibrary(string targetPath)
    {
        using Stream resourceStream = OpenResourceStream();
        using StreamReader reader = new(resourceStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
        string base64 = reader.ReadToEnd().Trim();
        byte[] compressedBytes = Convert.FromBase64String(base64);

        using MemoryStream compressedStream = new(compressedBytes);
        using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);
        using FileStream output = File.Create(targetPath);
        gzipStream.CopyTo(output);
    }

    private static Stream OpenResourceStream()
    {
        string resourcePath = Path.Combine(AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory, "lib", "linux-x64", "libDetour.so.gz.b64");

        if (File.Exists(resourcePath))
            return File.OpenRead(resourcePath);

        Assembly assembly = typeof(DetourLibraryInstaller).Assembly;
        const string logicalName = "Detour.libDetour.so.gz.b64";
        Stream? stream = assembly.GetManifestResourceStream(logicalName);

        if (stream is not null)
            return stream;

        throw new FileNotFoundException("Unable to locate Detour native library payload.");
    }
}
