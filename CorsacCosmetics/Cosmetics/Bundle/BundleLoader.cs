using System;
using System.IO;
using System.Text.Json;
using CorsacCosmetics.Cosmetics.Hats;
using CorsacCosmetics.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CorsacCosmetics.Cosmetics.Bundle;

public class BundleLoader(HatLoader hatLoader)
{
    public static string Normalize(string name, string type)
    {
        return $"corsac.bundle.{type}.{name.ToLower().Replace(" ", "_")}";
    }

    public void LoadBundles(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.ccb"))
        {
            try
            {
                Info($"Loading bundle from {file}");
                LoadBundle(file);
            }
            catch (Exception e)
            {
                Error($"Error loading bundle from {file}:\n{e.ToString()}");
            }
        }
    }

    public void LoadBundle(string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException();
        }

        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
        var header = BundleHeader.Read(fs);

        if (!header.IsValid)
        {
            throw new InvalidHeaderException("Bundle header is invalid.");
        }

        if (!header.IsSupportedVersion)
        {
            throw new UnsupportedVersionException($"Bundle version {header.Version} is not supported!");
        }

        var manifestBytes = new byte[header.ManifestLength];
        if (fs.Read(manifestBytes.AsSpan()) != header.ManifestLength)
        {
            throw new EndOfStreamException("Could not read full bundle manifest!");
        }

        var manifest = JsonSerializer.Deserialize<BundleManifest>(manifestBytes);
        if (manifest.Hats == null)
        {
            throw new InvalidDataException("Bundle data cannot be null!");
        }

        var start = fs.Position;

        foreach (var hatManifest in manifest.Hats)
        {
            LoadHat(hatManifest, fs, start);
            Info($"Loaded {hatManifest.Name} from bundle");
        }
    }

    private void LoadHat(HatManifest manifest, FileStream fs, long start)
    {
        var id = Normalize(manifest.Name, "hat");

        var hatViewData = ScriptableObject.CreateInstance<HatViewData>();
        hatViewData.name = manifest.Name;
        hatViewData.MatchPlayerColor = manifest.MatchPlayerColor;
        hatViewData.MainImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.MainSprite.Offset, manifest.MainSprite.Size);
        hatViewData.BackImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.BackSprite.Offset, manifest.BackSprite.Size);
        hatViewData.ClimbImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.ClimbSprite.Offset, manifest.ClimbSprite.Size);
        hatViewData.FloorImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.FloorSprite.Offset, manifest.FloorSprite.Size);
        hatViewData.LeftMainImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.LeftMainSprite.Offset, manifest.LeftMainSprite.Size);
        hatViewData.LeftBackImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.LeftBackSprite.Offset, manifest.LeftBackSprite.Size);
        hatViewData.LeftClimbImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.LeftClimbSprite.Offset, manifest.LeftClimbSprite.Size);
        hatViewData.LeftFloorImage = SpriteTools.LoadSpriteFromStream(fs, start + manifest.LeftFloorSprite.Offset, manifest.LeftFloorSprite.Size);

        var previewData = ScriptableObject.CreateInstance<PreviewViewData>();
        previewData.name = manifest.Name;
        previewData.PreviewSprite = SpriteTools.LoadSpriteFromStream(fs, start + manifest.PreviewSprite.Offset, manifest.PreviewSprite.Size);

        var hatData = ScriptableObject.CreateInstance<HatData>();
        hatData.name = hatData.StoreName = manifest.Name;
        hatData.Free = true;
        hatData.ProductId = id;
        hatData.BlocksVisors = manifest.BlocksVisors;
        hatData.NoBounce = manifest.NoBounce;
        hatData.InFront = manifest.InFront;
        hatData.ViewDataRef = new AssetReference(HatLocator.GetGuid(id, ReferenceType.HatViewData));
        hatData.PreviewData = new AssetReference(HatLocator.GetGuid(id, ReferenceType.Preview));

        var customHat = new CustomHat(id, hatData, hatViewData, previewData);
        hatLoader.CustomHats.Add(id, customHat);

        hatData.ViewDataRef.LoadAsset<HatViewData>();
        hatData.PreviewData.LoadAsset<PreviewViewData>();
    }
}