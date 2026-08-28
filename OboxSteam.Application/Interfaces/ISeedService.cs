namespace OboxSteam.Application.Interfaces
{
    public interface ISeedService
    {
        Task SeedAllDataAsync();

        Task SeedWs7FeTestDataAsync();

        Task ClearAllDataAsync();
    }
}
