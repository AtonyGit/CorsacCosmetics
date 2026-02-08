using System.Text.Json.Serialization;

namespace CosmeticCreator;

[JsonSerializable(typeof(BundleManifest))]
public partial class BundleManifestContext : JsonSerializerContext;

public struct BundleManifest()
{
    public const uint CurrentVersion = 1;

    [JsonIgnore]
    public bool IsValid => Version is > 0 and <= CurrentVersion;

    public uint Version { get; set; } = 0;
    public HatManifest[] Hats { get; set; } = [];

    public override string ToString()
    {
        return $"BundleManifest (Version {Version}, Hats: {Hats.Length})";
    }
}

public struct HatManifest()
{
    public string Name { get; set; } = "Custom Hat";
    public bool MatchPlayerColor { get; set; }
    public bool BlocksVisors { get; set; }
    public bool InFront { get; set; } = true;
    public bool NoBounce { get; set; } = true;

    public SpriteData PreviewSprite { get; set; } = new();
    public SpriteData MainSprite { get; set; } = new();
    public SpriteData BackSprite { get; set; } = new();
    public SpriteData ClimbSprite { get; set; } = new();
    public SpriteData FloorSprite { get; set; } = new();
    public SpriteData LeftMainSprite { get; set; } = new();
    public SpriteData LeftBackSprite { get; set; } = new();
    public SpriteData LeftClimbSprite { get; set; } = new();
    public SpriteData LeftFloorSprite { get; set; } = new();
}

public struct SpriteData()
{
    public uint Size { get; set; } = 0;
    public uint Offset { get; set; } = 0;

    [JsonIgnore]
    public bool HasData => Size > 0;
}
