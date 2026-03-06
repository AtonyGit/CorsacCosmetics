using UnityEngine;

namespace CorsacCosmetics.Cosmetics.Nameplates;

public class CustomNamePlate
{
    public CustomNamePlate(
        string id,
        NamePlateData namePlateData,
        NamePlateViewData namePlateViewData,
        PreviewViewData previewData
        )
    {
        Id = id;
        NamePlateData = namePlateData;
        NamePlateViewData = namePlateViewData;
        PreviewData = previewData;
    }

    public string Id { get; }

    public NamePlateData NamePlateData { get; }

    public NamePlateViewData NamePlateViewData { get; }

    public PreviewViewData PreviewData { get; }
}