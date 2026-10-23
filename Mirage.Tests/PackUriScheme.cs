using System.IO.Packaging;
using System.Runtime.CompilerServices;

namespace Mirage.Tests;

internal static class PackUriScheme
{
    [ModuleInitializer]
    internal static void Register() => _ = PackUriHelper.UriSchemePack;
}
