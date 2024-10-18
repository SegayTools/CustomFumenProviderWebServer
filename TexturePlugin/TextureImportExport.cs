using AssetsTools.NET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TexturePlugin
{
    public class TextureImportExport
    {
        public static bool ExportPng(byte[] encData, string file, int width, int height, TextureFormat format)
        {
            byte[] decData = TextureEncoderDecoder.Decode(encData, width, height, format);
            if (decData == null)
                return false;

            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(decData, width, height);
            image.Mutate(i => i.Flip(FlipMode.Vertical));
            image.SaveAsPng(file);

            return true;
        }
    }
}