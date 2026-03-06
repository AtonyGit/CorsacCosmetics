namespace CorsacCosmetics.Cosmetics.Hats;

public class CustomHat
{
    public CustomHat(
        string id,
        HatData hatData,
        HatViewData viewData,
        PreviewViewData previewData
        )
    {
        Id = id;
        HatData = hatData;
        HatViewData = viewData;
        PreviewData = previewData;
    }

    public string Id { get; }
    public HatData HatData { get; }
    public HatViewData HatViewData { get; }
    public PreviewViewData PreviewData { get; }
}