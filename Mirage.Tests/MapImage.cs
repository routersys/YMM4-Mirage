using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace Mirage.Tests;

internal sealed class MapImage : IDisposable
{
    public ID2D1Bitmap1 Bitmap { get; }

    public MapImage(IGraphicsDevicesAndContext devices, float[] texels)
    {
        var handle = GCHandle.Alloc(texels, GCHandleType.Pinned);
        try
        {
            Bitmap = devices.DeviceContext.CreateBitmap(
                new SizeI(MirageSettings.MapWidth, 1),
                handle.AddrOfPinnedObject(),
                MirageSettings.MapWidth * MirageSettings.TexelBytes,
                new BitmapProperties1(new PixelFormat(Format.R32G32B32A32_Float, Vortice.DCommon.AlphaMode.Premultiplied), SourceImage.Dpi, SourceImage.Dpi, BitmapOptions.None));
        }
        finally
        {
            handle.Free();
        }
    }

    public static MapImage Constant(IGraphicsDevicesAndContext devices, float shift)
        => new(devices, MirageTable.ToTexels(Enumerable.Repeat(shift, MirageSettings.MapWidth).ToArray()));

    public static MapImage Sloped(IGraphicsDevicesAndContext devices, float slope)
        => new(devices, MirageTable.ToTexels(Enumerable.Range(0, MirageSettings.MapWidth).Select(index => (float)(slope * MirageSettings.GetMapAngleRadians(index))).ToArray()));

    public void Dispose() => Bitmap.Dispose();
}
