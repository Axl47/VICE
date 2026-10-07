using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.TV;

internal static class TVArtwork
{
    internal static byte[] Import(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg")) throw new InvalidDataException("Choose a PNG or JPEG.");
        MediaRules.FileName(name, extension);
        var path = Path.Combine(TVConfig.Images, name);
        var info=new FileInfo(path);
        if ((info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Put a regular image file directly in the designated Images folder.");
        if (info.Length > 8 * 1024 * 1024) throw new InvalidDataException("Source images must be no larger than 8 MiB.");
        var bytes = File.ReadAllBytes(path); var dimensions = ImageDimensions.Read(bytes);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(source, new Il2CppStructArray<byte>(bytes), false);
            if (source.width != dimensions.Width || source.height != dimensions.Height) throw new InvalidDataException("Image could not be decoded.");
            for (var limit = 2048; limit >= 256; limit /= 2)
            {
                var scale = Math.Min(1f, limit / (float)Math.Max(source.width, source.height));
                var width = Math.Max(1, (int)(source.width * scale)); var height = Math.Max(1, (int)(source.height * scale));
                var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32); target.Create();
                var result = new Texture2D(width, height, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(source, target); RenderTexture.active = target;
                    result.ReadPixels(new Rect(0,0,width,height), 0,0,false); result.Apply(false,false);
                    var png = ImageConversion.EncodeToPNG(result).ToArray();
                    if (png.Length <= MediaRules.MaxImageBytes) return png;
                }
                finally { RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(result); }
            }
            throw new InvalidDataException("Image remains too large after resizing.");
        }
        finally { Object.Destroy(source); }
    }
    internal static Texture2D Load(byte[] bytes)
    {
        ImageDimensions.Read(bytes);
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
        ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(bytes), false);
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }
}
