using System.Text.Json;

namespace BodySim.Tests;

public class BodyTests
{
    [Fact]
    public void Constructor_InitializesRespiratorySystem()
    {
        var body = new Body();

        Assert.NotNull(body.GetSystem(BodySystemType.Respiratory));
    }

    [Fact]
    public void ExportForGodotJson_ContainsSystemsAndResources()
    {
        var body = new Body();

        using var document = JsonDocument.Parse(body.ExportForGodotJson());
        var root = document.RootElement;

        Assert.True(root.GetProperty("resources").TryGetProperty(BodyResourceType.Blood.ToString(), out _));

        var systems = root.GetProperty("systems");
        Assert.True(systems.TryGetProperty(BodySystemType.Skeletal.ToString(), out _));
        Assert.True(systems.TryGetProperty(BodySystemType.Circulatory.ToString(), out _));
        Assert.True(systems.TryGetProperty(BodySystemType.Respiratory.ToString(), out var respiratory));
        Assert.True(respiratory.TryGetProperty(BodyPartType.Chest.ToString(), out var chest));
        Assert.True(chest.GetProperty("components").TryGetProperty(BodyComponentType.AirFlow.ToString(), out _));
    }
}
