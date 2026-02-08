using System.Text.Json.Serialization;

namespace CorsacCosmetics.Cosmetics.Bundle;

[JsonSerializable(typeof(BundleManifest))]
public partial class BundleManifestContext : JsonSerializerContext;

public struct BundleManifest()
{
    public const uint CurrentVersion = 1;
    public bool IsValid => Version is > 0 and <= CurrentVersion;

    public uint Version = 0;
    public HatManifest[] Hats = [];
}

public struct HatManifest()
{
    public string Name = "Custom Hat";
    public bool MatchPlayerColor;
    public bool BlocksVisors;
    public bool InFront = true;
    public bool NoBounce = true;

    public SpriteData PreviewSprite = new();
    public SpriteData MainSprite = new();
    public SpriteData BackSprite = new();
    public SpriteData ClimbSprite = new();
    public SpriteData FloorSprite = new();
    public SpriteData LeftMainSprite = new();
    public SpriteData LeftBackSprite = new();
    public SpriteData LeftClimbSprite = new();
    public SpriteData LeftFloorSprite = new();
}

public struct SpriteData()
{
    public uint Size = 0;
    public uint Offset = 0;
    public bool HasData => Size > 0;
}
