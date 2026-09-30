using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SeedServiceClearTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IBlobService> _blob = new();
    private readonly Mock<ICertificateService> _certificates = new();
    private readonly Mock<IFaceRecognitionService> _faces = new();

    public SeedServiceClearTests()
    {
        _blob
            .Setup(b => b.ClearAllObjectsExceptPrefixAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0));
        _faces
            .Setup(f => f.ResetCollectionAfterAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());
    }

    [Fact]
    public async Task ClearAllData_PurgesFacesAroundS3AndDatabase()
    {
        var order = new List<string>();
        _faces
            .Setup(f => f.ResetCollectionAfterAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                order.Add("faces-before");
                await action();
                order.Add("faces-after");
            });
        _blob
            .Setup(b => b.ClearAllObjectsExceptPrefixAsync(
                "Seed/",
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("s3"))
            .ReturnsAsync((0, 0));
        _db.OnTruncate = () => order.Add("db");

        await CreateSut().ClearAllDataAsync();

        Assert.Equal(new[] { "faces-before", "s3", "db", "faces-after" }, order);
        _faces.Verify(f => f.ResetCollectionAfterAsync(It.IsAny<Func<Task>>()), Times.Once);
        Assert.Equal(1, _db.TruncateCallCount);
    }

    [Fact]
    public async Task ClearAllData_StopsWhenFaceCollectionPurgeFails()
    {
        _faces
            .Setup(f => f.ResetCollectionAfterAsync(It.IsAny<Func<Task>>()))
            .ThrowsAsync(new InvalidOperationException("rekognition unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ClearAllDataAsync());

        _blob.Verify(
            b => b.ClearAllObjectsExceptPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(0, _db.TruncateCallCount);
    }

    private SeedService CreateSut() =>
        new(
            NullLogger<SeedService>.Instance,
            _db,
            _blob.Object,
            _certificates.Object,
            _faces.Object);
}
