using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class PngDateTakenWriterTests
    {
        private static readonly DateTime Taken = new DateTime(2026, 9, 27, 14, 30, 52);

        [Fact]
        public void IHDRの直後にeXIfとiTXtが入りIDATより前にある()
        {
            byte[] result = PngDateTakenWriter.InsertDateTaken(CreatePng(), Taken);

            var types = ReadChunkTypes(result);
            Assert.Equal("IHDR", types[0]);
            Assert.Equal("eXIf", types[1]);
            Assert.Equal("iTXt", types[2]);
            Assert.True(types.IndexOf("iTXt") < types.IndexOf("IDAT"));
            Assert.Equal("IEND", types[^1]);
        }

        [Fact]
        public void 全チャンクのCRCが正しい()
        {
            byte[] result = PngDateTakenWriter.InsertDateTaken(CreatePng(), Taken);

            int pos = 8;
            while (pos < result.Length)
            {
                int len = ReadInt32BigEndian(result, pos);
                uint expected = (uint)ReadInt32BigEndian(result, pos + 8 + len);
                Assert.Equal(expected, PngDateTakenWriter.Crc32(result, pos + 4, 4 + len));
                pos += 12 + len;
            }
        }

        [Fact]
        public void 挿入後もPNGとしてデコードできる()
        {
            byte[] result = PngDateTakenWriter.InsertDateTaken(CreatePng(), Taken);

            using var ms = new MemoryStream(result);
            var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(4, frame.PixelWidth);
            Assert.Equal(3, frame.PixelHeight);
        }

        [Fact]
        public void ExifにはExifIFD経由でDateTimeOriginalが入る()
        {
            byte[] tiff = PngDateTakenWriter.BuildExifTiff(Taken);

            Assert.Equal("II", Encoding.ASCII.GetString(tiff, 0, 2));
            Assert.Equal(42, BitConverter.ToUInt16(tiff, 2));

            int ifd0 = (int)BitConverter.ToUInt32(tiff, 4);
            Assert.Equal(0x8769, BitConverter.ToUInt16(tiff, ifd0 + 2));
            int exifIfd = (int)BitConverter.ToUInt32(tiff, ifd0 + 2 + 8);

            Assert.Equal(0x9003, BitConverter.ToUInt16(tiff, exifIfd + 2));
            int count = (int)BitConverter.ToUInt32(tiff, exifIfd + 2 + 4);
            int valueOffset = (int)BitConverter.ToUInt32(tiff, exifIfd + 2 + 8);
            Assert.Equal("2026:09:27 14:30:52\0", Encoding.ASCII.GetString(tiff, valueOffset, count));
        }

        [Fact]
        public void XMPのiTXtに撮影日時が入る()
        {
            byte[] data = PngDateTakenWriter.BuildXmpITxt(Taken);

            string keyword = Encoding.ASCII.GetString(data, 0, PngDateTakenWriter.XmpKeyword.Length);
            Assert.Equal("XML:com.adobe.xmp", keyword);
            // キーワード終端・圧縮フラグ・圧縮方式・言語タグ終端・翻訳キーワード終端の5バイトはすべて0
            Assert.All(data.Skip(keyword.Length).Take(5), b => Assert.Equal(0, b));

            int textStart = keyword.Length + 5;
            string xmp = Encoding.UTF8.GetString(data, textStart, data.Length - textStart);
            Assert.Contains("<exif:DateTimeOriginal>2026-09-27T14:30:52</exif:DateTimeOriginal>", xmp);
            Assert.Contains("<xmp:CreateDate>2026-09-27T14:30:52</xmp:CreateDate>", xmp);
        }

        [Fact]
        public void PNGでないバイト列は拒否する()
        {
            Assert.Throws<ArgumentException>(() => PngDateTakenWriter.InsertDateTaken(new byte[64], Taken));
        }

        private static byte[] CreatePng()
        {
            var bmp = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 3 * 4], 4 * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        private static List<string> ReadChunkTypes(byte[] png)
        {
            var types = new List<string>();
            int pos = 8;
            while (pos < png.Length)
            {
                int len = ReadInt32BigEndian(png, pos);
                types.Add(Encoding.ASCII.GetString(png, pos + 4, 4));
                pos += 12 + len;
            }
            return types;
        }

        private static int ReadInt32BigEndian(byte[] b, int o)
            => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
    }
}
