using System.IO.Compression;

namespace X4SaveZoneCleaner;

internal static class SelfTest
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "X4SaveZoneCleaner-selftest-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "save_001.xml.gz");
            const string xml =
                "<save><component class=\"zone\" macro=\"tempzone\" code=\"NQT-989\" id=\"[0x1]\"><offset><position x=\"100\" y=\"0\" z=\"-2000000\"/></offset><component class=\"ship_s\"/></component><component class=\"zone\" macro=\"tempzone\" code=\"YWT-712\" id=\"[0x2]\"><offset><position x=\"-59499700\" y=\"28.977\" z=\"-45705060\"/></offset><component class=\"ship_s\"/></component><component class=\"zone\" code=\"SAFE\" id=\"[0x3]\"><offset><position x=\"10\" y=\"0\" z=\"10\"/></offset></component></save>";
            using (var file = File.Create(path))
            using (var gzip = new GZipStream(file, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip))
                writer.Write(xml);
            var save = SaveDocument.Load(path);
            var zones = save.FindSuspiciousZones(1_000_000);
            if (!zones.Select(x => x.Code).OrderBy(x => x).SequenceEqual(new[] { "NQT-989", "YWT-712" }))
                throw new Exception("Expected NQT-989 and YWT-712 were not detected.");
            var output = Path.Combine(directory, "cleaned.xml.gz");
            save.WriteCleanedCopy(zones.Where(x => x.Code == "YWT-712"), output, makeBackup: true);
            var cleaned = SaveDocument.Load(output);
            if (cleaned.Xml.ToString().Contains("YWT-712") || !cleaned.Xml.ToString().Contains("NQT-989"))
                throw new Exception("Selective removal validation failed.");
            Console.WriteLine("Self-test passed: detection and selective XML removal are valid.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
