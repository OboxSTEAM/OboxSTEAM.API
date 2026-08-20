namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private DateTime _seedNow;

    private DateTime AtDays(int days) => _seedNow.AddDays(days);

    private DateTime AtMonths(int months) => _seedNow.AddMonths(months);
}
