using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace X4SaveZoneCleaner;

/// <summary>Loads X4 save XML, identifies suspicious zone components, and writes a new copy only.</summary>
public sealed class SaveDocument
{
    private readonly byte[] _sourceBytes;
    public string SourcePath { get; }
    public bool IsGzip { get; }
    public XDocument Xml { get; }

    private SaveDocument(string sourcePath, byte[] sourceBytes, bool isGzip, XDocument xml)
    {
        SourcePath = sourcePath;
        _sourceBytes = sourceBytes;
        IsGzip = isGzip;
        Xml = xml;
    }

    public static SaveDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var gzip = bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
        using var input = new MemoryStream(bytes);
        using Stream decoded = gzip ? new GZipStream(input, CompressionMode.Decompress) : input;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(decoded, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        return new SaveDocument(path, bytes, gzip, document);
    }

    public IReadOnlyList<ZoneRecord> FindSuspiciousZones(double thresholdMetres)
    {
        return Xml.Descendants("component")
            .Where(IsZone)
            .Select(z => CreateRecord(z, thresholdMetres))
            .Where(r => r is not null)
            .Cast<ZoneRecord>()
            .OrderByDescending(r => Math.Max(Math.Abs(r.X), Math.Max(Math.Abs(r.Y), Math.Abs(r.Z))))
            .ToList();
    }

    public string WriteCleanedCopy(IEnumerable<ZoneRecord> selected, string outputPath, bool makeBackup)
    {
        var selectedKeys = selected.Select(x => (x.Id, x.Code)).ToHashSet();
        var copy = new XDocument(Xml);
        var matches = copy.Descendants("component").Where(IsZone)
            .Where(x => selectedKeys.Contains(((string?)x.Attribute("id") ?? "", (string?)x.Attribute("code") ?? "")))
            .ToList();

        if (matches.Count != selectedKeys.Count)
            throw new InvalidOperationException("At least one selected zone was no longer found. No file was written.");
        if (matches.Count == 0)
            throw new InvalidOperationException("Select at least one zone.");

        foreach (var element in matches) element.Remove();
        Validate(copy);

        if (makeBackup)
        {
            var backup = Path.Combine(Path.GetDirectoryName(outputPath)!,
                Path.GetFileNameWithoutExtension(outputPath) + ".source-backup" + (IsGzip ? ".gz" : ".xml"));
            File.WriteAllBytes(UniquePath(backup), _sourceBytes);
        }

        using var file = File.Create(outputPath);
        Stream target = file;
        if (IsGzip) target = new GZipStream(file, CompressionLevel.Optimal, leaveOpen: false);
        using (target)
        using (var writer = XmlWriter.Create(target, new XmlWriterSettings { Indent = false, Encoding = new System.Text.UTF8Encoding(false) }))
            copy.Save(writer);

        return outputPath;
    }

    /// <summary>Returns the complete zone elements selected for removal without changing the save.</summary>
    public string GetRemovalPreview(IEnumerable<ZoneRecord> selected)
    {
        var selectedKeys = selected.Select(x => (x.Id, x.Code)).ToHashSet();
        var matches = Xml.Descendants("component").Where(IsZone)
            .Where(x => selectedKeys.Contains(((string?)x.Attribute("id") ?? "", (string?)x.Attribute("code") ?? "")))
            .ToList();
        if (matches.Count != selectedKeys.Count)
            throw new InvalidOperationException("At least one selected zone was no longer found.");
        if (matches.Count == 0)
            throw new InvalidOperationException("Select at least one zone.");

        return string.Join(Environment.NewLine + Environment.NewLine,
            matches.Select(x => $"<!-- Zone to be removed: {(string?)x.Attribute("code") ?? "(no code)"} {(string?)x.Attribute("id") ?? "(no id)"} -->{Environment.NewLine}{x.ToString()}"));
    }

    public string DefaultOutputPath()
    {
        var folder = Path.GetDirectoryName(SourcePath)!;
        var baseName = Path.GetFileNameWithoutExtension(SourcePath);
        return UniquePath(Path.Combine(folder, baseName + (IsGzip ? ".gz" : ".xml")));
    }

    private static bool IsZone(XElement e) =>
        string.Equals((string?)e.Attribute("class"), "zone", StringComparison.OrdinalIgnoreCase);

    private static ZoneRecord? CreateRecord(XElement zone, double threshold)
    {
        var position = zone.Element("offset")?.Element("position") ?? zone.Descendants("position").FirstOrDefault();
        if (position is null) return null;
        if (!TryCoordinate(position, "x", out var x) || !TryCoordinate(position, "y", out var y) || !TryCoordinate(position, "z", out var z)) return null;
        var maximum = new[] { Math.Abs(x), Math.Abs(y), Math.Abs(z) }.Max();
        if (maximum < threshold) return null;

        // Do not count objects from a child zone as if they belonged to this zone.
        var contents = DescendantsUntilNestedZone(zone).ToList();
        var ships = contents.Count(e => ((string?)e.Attribute("class") ?? "").StartsWith("ship", StringComparison.OrdinalIgnoreCase));
        var stations = contents.Count(e => ((string?)e.Attribute("class") ?? "").StartsWith("station", StringComparison.OrdinalIgnoreCase));
        var id = (string?)zone.Attribute("id") ?? "(no id)";
        var code = (string?)zone.Attribute("code") ?? "(no code)";
        return new ZoneRecord(id, code, x, y, z, ships, stations, $"coordinate exceeds {threshold:N0} m");
    }

    private static IEnumerable<XElement> DescendantsUntilNestedZone(XElement root)
    {
        foreach (var child in root.Elements())
        {
            if (child.Name == "component" && IsZone(child)) continue;
            yield return child;
            foreach (var descendant in DescendantsUntilNestedZone(child)) yield return descendant;
        }
    }

    private static bool TryCoordinate(XElement element, string name, out double value) =>
        double.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static void Validate(XDocument document)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer); // forces the XML writer to validate the remaining tree
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name}.{i}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
