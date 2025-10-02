using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DOL.GS
{
    internal static class BuildInfo
    {
        private static readonly string _stamp = ResolveBuildStamp();

        public static string Stamp => _stamp;

        private static string ResolveBuildStamp()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string? attributeStamp = assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Key, "BuildTime", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (!string.IsNullOrEmpty(attributeStamp))
                    return attributeStamp;

                string location = assembly.Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    DateTime timestamp = File.GetLastWriteTimeUtc(location);
                    return timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                }
            }
            catch
            {
                // Fall through to return fallback value.
            }

            return "unknown";
        }
    }
}
