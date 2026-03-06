namespace CorsacCosmetics.Cosmetics.Visors;

public class CustomVisor
{
    public CustomVisor(
        string id,
        VisorData visorData,
        VisorViewData visorViewData,
        PreviewViewData previewData
        )
    {
        Id = id;
        VisorData = visorData;
        VisorViewData = visorViewData;
        PreviewData = previewData;
    }

    public string Id { get; }

    public VisorData VisorData { get; }

    public VisorViewData VisorViewData { get; }

    public PreviewViewData PreviewData { get; }
}