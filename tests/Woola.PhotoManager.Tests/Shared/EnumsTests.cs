using Woola.PhotoManager.Shared.Enums;

namespace Woola.PhotoManager.Tests.Shared;

public class EnumsTests
{
    [Fact]
    public void AgentName_AllHaveDisplayNames()
    {
        foreach (AgentName agent in Enum.GetValues<AgentName>())
        {
            var display = agent.ToDisplayName();
            Assert.False(string.IsNullOrEmpty(display));
        }
    }

    [Fact]
    public void AgentName_AllHaveDescriptions()
    {
        foreach (AgentName agent in Enum.GetValues<AgentName>())
        {
            var desc = agent.ToDescription();
            Assert.False(string.IsNullOrEmpty(desc));
        }
    }

    [Fact]
    public void SmartAlbumId_AllHaveRouteStrings()
    {
        foreach (SmartAlbumId id in Enum.GetValues<SmartAlbumId>())
        {
            var route = id.ToRouteString();
            Assert.False(string.IsNullOrEmpty(route));
        }
    }

    [Fact]
    public void SmartAlbumId_AllHaveDisplayNames()
    {
        foreach (SmartAlbumId id in Enum.GetValues<SmartAlbumId>())
        {
            var name = id.ToDisplayName();
            Assert.False(string.IsNullOrEmpty(name));
        }
    }
}
