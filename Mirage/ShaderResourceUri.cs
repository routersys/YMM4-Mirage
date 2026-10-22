namespace Mirage;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/Mirage;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
