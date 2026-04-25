namespace CorsacCosmetics.Cosmetics;

public static class Names
{
    public static string Normalize(
        string name,
        string type,
        string group = "default"
    )
    {
        return $"corsac.{group}.{type}.{name.ToLower().Replace(" ", "_")}";
    }
}