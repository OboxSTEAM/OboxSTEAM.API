namespace OboxSteam.Application.Commons;

public sealed record FrameworkVersionInfo(int? Current, int? Latest)
{
    public bool HasNewer => Latest.HasValue && (!Current.HasValue || Latest.Value > Current.Value);
}
