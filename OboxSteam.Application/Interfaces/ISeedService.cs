namespace OboxSteam.Application.Interfaces
{
    public interface ISeedService
    {
        Task SeedAllDataAsync();
        Task ClearAllDataAsync();
    }
}
