namespace X4SaveZoneCleaner;

public sealed record ZoneRecord(
    string Id,
    string Code,
    double X,
    double Y,
    double Z,
    int ShipCount,
    int StationCount,
    string Reason)
{
    public string Position => $"{X:N0} / {Y:N0} / {Z:N0} m";
    public string Content => $"{ShipCount} ship(s), {StationCount} station(s)";
}
