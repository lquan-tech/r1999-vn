using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIco
{
    static void Main(string[] args)
    {
        string inputPng = args.Length > 0 ? args[0] : "public/icon.png";
        string outputIco = args.Length > 1 ? args[1] : "public/reverse1999-icon.ico";

        using (Bitmap src = new Bitmap(inputPng))
        {
            int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
            byte[][] imagesData = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                using (Bitmap resized = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(src, 0, 0, s, s);
                    }

                    if (s <= 128)
                    {
                        // Write as standard DIB/BMP format for maximum Windows taskbar compatibility
                        imagesData[i] = CreateBitmapEntry(resized);
                    }
                    else
                    {
                        // Write 256 as PNG
                        using (MemoryStream ms = new MemoryStream())
                        {
                            resized.Save(ms, ImageFormat.Png);
                            imagesData[i] = ms.ToArray();
                        }
                    }
                }
            }

            using (FileStream fs = new FileStream(outputIco, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // ICONDIR header
                bw.Write((short)0); // reserved
                bw.Write((short)1); // icon type
                bw.Write((short)sizes.Length); // count

                int offset = 6 + (16 * sizes.Length);

                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s)); // width
                    bw.Write((byte)(s >= 256 ? 0 : s)); // height
                    bw.Write((byte)0); // color count
                    bw.Write((byte)0); // reserved
                    bw.Write((short)1); // color planes
                    bw.Write((short)32); // bits per pixel
                    bw.Write(imagesData[i].Length); // bytes in res
                    bw.Write(offset); // image offset
                    offset += imagesData[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(imagesData[i]);
                }
            }
        }

        Console.WriteLine("Successfully created multi-res icon: " + outputIco);
    }

    static byte[] CreateBitmapEntry(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        int imageSize = width * height * 4;
        int maskSize = ((width + 31) / 32) * 4 * height;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            // BITMAPINFOHEADER (40 bytes)
            bw.Write(40); // biSize
            bw.Write(width); // biWidth
            bw.Write(height * 2); // biHeight (XOR + AND mask)
            bw.Write((short)1); // biPlanes
            bw.Write((short)32); // biBitCount
            bw.Write(0); // biCompression (BI_RGB)
            bw.Write(imageSize + maskSize); // biSizeImage
            bw.Write(0); // biXPelsPerMeter
            bw.Write(0); // biYPelsPerMeter
            bw.Write(0); // biClrUsed
            bw.Write(0); // biClrImportant

            // Pixel data: bottom-to-top, BGRA
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B);
                    bw.Write(c.G);
                    bw.Write(c.R);
                    bw.Write(c.A);
                }
            }

            // AND mask (all 0 for 32bpp alpha)
            byte[] mask = new byte[maskSize];
            bw.Write(mask);

            return ms.ToArray();
        }
    }
}
