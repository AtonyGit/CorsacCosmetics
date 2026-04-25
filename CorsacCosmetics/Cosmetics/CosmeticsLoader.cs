using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CorsacCosmetics.Cosmetics.Bundle;
using CorsacCosmetics.Cosmetics.Bundle.V2;
using CorsacCosmetics.Cosmetics.Hats;
using CorsacCosmetics.Cosmetics.Nameplates;
using CorsacCosmetics.Cosmetics.Visors;
using CorsacCosmetics.Unity;
using Il2CppInterop.Runtime;
using Il2CppSystem.IO;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace CorsacCosmetics.Cosmetics;

public class CosmeticsLoader
{
    private static CosmeticsLoader? _cosmeticsLoader;
    public static CosmeticsLoader Instance => _cosmeticsLoader ??= new CosmeticsLoader();

    private readonly Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object> _emptyKeys = new();

    public Il2CppSystem.Collections.Generic.IEnumerable<Il2CppSystem.Object> EmptyKeys { get; }

    private CosmeticReleaseGroup CosmeticGroup { get; }

    // ID -> Name
    private Dictionary<string, string> CustomGroups { get; }

    // used to prevent groups with empty # of certain elements showing in inventory
    public List<string> HatGroups { get; } = [];
    public List<string> VisorGroups { get; } = [];
    public List<string> NameplateGroups { get; } = [];

    private readonly BundleLoader _bundleLoader;
    private readonly BundleLoaderV2 _bundleLoaderV2;

    private HatLoader HatLoader { get; } = new();
    private VisorLoader VisorLoader { get; } = new();
    private NameplateLoader NameplateLoader { get; } = new();

    private CosmeticsLoader()
    {
        EmptyKeys = new Il2CppSystem.Collections.Generic.IEnumerable<Il2CppSystem.Object>(_emptyKeys.Pointer);
        CosmeticGroup = ScriptableObject.CreateInstance<CosmeticReleaseGroup>();
        CosmeticGroup.date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        CustomGroups = [];
        CustomGroups.Add("default", "Custom Cosmetics");

        _bundleLoader = new BundleLoader(HatLoader, VisorLoader, NameplateLoader);
        _bundleLoaderV2 = new BundleLoaderV2(HatLoader, VisorLoader, NameplateLoader, CustomGroups);
    }

    public string GetHatGroupIdByIndex(int index)
    {
        return HatGroups[index];
    }

    public string GetHatGroupNameByIndex(int index)
    {
        return CustomGroups[HatGroups[index]];
    }

    public void LoadCosmetics()
    {
        Info("Loading bundles...");
        foreach (var file in Directory.GetFiles(CosmeticPaths.BundlePath, "*.ccb"))
        {
            if (file == null) continue;
            var v1Loaded = false;
            try
            {
                v1Loaded = _bundleLoader.LoadBundle(file);
            }
            catch (Exception e)
            {
                Error($"Error while loading bundle {file} with v1 loader:\n{e}");
            }

            if (v1Loaded)
            {
                continue;
            }

            try
            {
                _bundleLoaderV2.LoadBundle(file);
            }
            catch (Exception e)
            {
                Error($"Error while loading bundle {file} with v2 loader:\n{e}");
            }
        }

        Info("Loading hats...");
        HatLoader.LoadCosmetics(CosmeticPaths.HatPath);

        Info("Loading visors...");
        VisorLoader.LoadCosmetics(CosmeticPaths.VisorPath);

        Info("Loading nameplates...");
        NameplateLoader.LoadCosmetics(CosmeticPaths.NameplatePath);

        Info("Setting up cosmetic group...");
        foreach (var id in HatLoader.CustomHats.Keys)
        {
            var group = Names.GetGroup(id);
            if (!HatGroups.Contains(group))
            { 
                HatGroups.Add(group);
            }

            CosmeticGroup.ids.Add(id);
        }
        foreach (var id in VisorLoader.CustomVisors.Keys)
        {
            var group = Names.GetGroup(id);
            if (!VisorGroups.Contains(group))
            {
                VisorGroups.Add(group);
            }

            CosmeticGroup.ids.Add(id);
        }
        foreach (var id in NameplateLoader.CustomNamePlates.Keys)
        {
            var group = Names.GetGroup(id);
            if (!NameplateGroups.Contains(group))
            {
                NameplateGroups.Add(group);
            }

            CosmeticGroup.ids.Add(id);
        }
    }

    public void InstallCosmetics(ReferenceData referenceData)
    {
        Info("Installing hats...");
        HatLoader.InstallCosmetics(referenceData);

        Info("Installing visors...");
        VisorLoader.InstallCosmetics(referenceData);

        Info("Installing nameplates");
        NameplateLoader.InstallCosmetics(referenceData);

        Info("Installing cosmetic group...");
        var newGroups = referenceData.Groups.releaseGroups.ToList();
        newGroups.Add(CosmeticGroup);
        referenceData.Groups.releaseGroups = newGroups.ToArray();
    }

    public bool LocateCosmetic(
        string id,
        string type,
        [NotNullWhen(true)] out Il2CppSystem.Type? il2CPPType
    )
    {
        il2CPPType = null;
        try
        {
            il2CPPType = type switch
            {
                ReferenceType.Preview => Il2CppType.Of<PreviewViewData>(),
                _ => null
            };

            return il2CPPType != null
                   || HatLoader.LocateCosmetic(id, type, out il2CPPType)
                   || VisorLoader.LocateCosmetic(id, type, out il2CPPType)
                   || NameplateLoader.LocateCosmetic(id, type, out il2CPPType);
        }
        catch (Exception e)
        {
            Error($"Unexpected error while locating cosmetic {id}:\n{e.ToString()}");
            return false;
        }
    }

    public bool ProvideCosmetic(
        ProvideHandle provideHandle,
        string id,
        string type,
        [NotNullWhen(false)] out Exception? exception
        )
    {
        exception = null;
        try
        {
            var result = 
                HatLoader.ProvideCosmetic(provideHandle, id, type) 
                || VisorLoader.ProvideCosmetic(provideHandle, id, type)
                || NameplateLoader.ProvideCosmetic(provideHandle, id, type);

            return result ? true : throw new Exception($"No cosmetic found for {id} and type {type}");
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }

    public bool TryGetHat(string id, [NotNullWhen(true)] out CustomHat? hat)
    {
        return HatLoader.CustomHats.TryGetValue(id, out hat);
    }

    public bool TryGetVisor(string id, [NotNullWhen(true)] out CustomVisor? visor)
    {
        return VisorLoader.CustomVisors.TryGetValue(id, out visor);
    }

    public bool TryGetNamePlate(string id, [NotNullWhen(true)] out CustomNamePlate? namePlate)
    {
        return NameplateLoader.CustomNamePlates.TryGetValue(id, out namePlate);
    }
}