using System;
using System.IO;
using System.Text;

namespace SCtoolGui
{
    /// <summary>
    /// PNG に撮影日時を差し込む。GDI+ の PNG エンコーダは PropertyItem を書き出さないため、
    /// エンコード後のバイト列へ自前でチャンクを挿入する。読み手によって見る場所が違うので2通り書く。
    /// ・eXIf チャンク（PNG 第3版で正式採用。exiftool・ブラウザ・Apple 系が読む）
    /// ・iTXt の XMP（Windows エクスプローラー/WIC は eXIf を読まず、こちらを「撮影日時」として読む）
    /// どちらも IDAT より前に置く必要があるため、IHDR の直後に入れる。
    /// </summary>
    public static class PngDateTakenWriter
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>シグネチャ(8) + IHDR チャンク(長さ4 + 種別4 + データ13 + CRC4)。</summary>
        private const int IhdrEnd = 8 + 4 + 4 + 13 + 4;

        /// <summary>PNG バイト列の IHDR 直後に、撮影日時入りの eXIf / XMP チャンクを挿入した新しいバイト列を返す。</summary>
        public static byte[] InsertDateTaken(byte[] png, DateTime taken)
        {
            if (png.Length < IhdrEnd || !png.AsSpan(0, 8).SequenceEqual(PngSignature)
                || Encoding.ASCII.GetString(png, 12, 4) != "IHDR")
            {
                throw new ArgumentException("PNG 形式ではありません。", nameof(png));
            }

            byte[] exif = BuildChunk("eXIf", BuildExifTiff(taken));
            byte[] xmp = BuildChunk("iTXt", BuildXmpITxt(taken));

            var result = new byte[png.Length + exif.Length + xmp.Length];
            Buffer.BlockCopy(png, 0, result, 0, IhdrEnd);
            Buffer.BlockCopy(exif, 0, result, IhdrEnd, exif.Length);
            Buffer.BlockCopy(xmp, 0, result, IhdrEnd + exif.Length, xmp.Length);
            Buffer.BlockCopy(png, IhdrEnd, result, IhdrEnd + exif.Length + xmp.Length, png.Length - IhdrEnd);
            return result;
        }

        /// <summary>XMP を PNG に入れる際の iTXt キーワード（XMP 仕様 Part 3 で規定）。</summary>
        public const string XmpKeyword = "XML:com.adobe.xmp";

        /// <summary>
        /// exif:DateTimeOriginal と xmp:CreateDate を持つ XMP パケットを、iTXt チャンクのデータ部として組み立てる。
        /// iTXt = キーワード\0 + 圧縮フラグ(0) + 圧縮方式(0) + 言語タグ\0 + 翻訳キーワード\0 + UTF-8 本文。
        /// </summary>
        public static byte[] BuildXmpITxt(DateTime taken)
        {
            string date = taken.ToString("yyyy-MM-ddTHH:mm:ss");
            string xmp =
                "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" +
                "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">" +
                "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
                "<rdf:Description rdf:about=\"\" xmlns:exif=\"http://ns.adobe.com/exif/1.0/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">" +
                $"<exif:DateTimeOriginal>{date}</exif:DateTimeOriginal>" +
                $"<xmp:CreateDate>{date}</xmp:CreateDate>" +
                "</rdf:Description></rdf:RDF></x:xmpmeta>" +
                "<?xpacket end=\"w\"?>";

            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes(XmpKeyword));
            ms.Write(new byte[] { 0, 0, 0, 0, 0 }); // キーワード終端, 非圧縮, 圧縮方式, 言語タグ空, 翻訳キーワード空
            ms.Write(Encoding.UTF8.GetBytes(xmp));
            return ms.ToArray();
        }

        /// <summary>
        /// DateTimeOriginal(0x9003) だけを持つ最小の Exif(TIFF, リトルエンディアン)を組み立てる。
        /// DateTimeOriginal は Exif サブIFD に属するため、IFD0 には Exif IFD へのポインタ(0x8769)だけを置く。
        /// </summary>
        public static byte[] BuildExifTiff(DateTime taken)
        {
            byte[] dateBytes = Encoding.ASCII.GetBytes(taken.ToString("yyyy:MM:dd HH:mm:ss") + "\0"); // 20バイト

            const int ifd0Offset = 8;
            const int exifIfdOffset = ifd0Offset + 2 + 12 + 4; // 26
            const int dataOffset = exifIfdOffset + 2 + 12 + 4;  // 44

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms); // BinaryWriter はリトルエンディアン

            // TIFF ヘッダ
            w.Write((byte)'I'); w.Write((byte)'I');
            w.Write((ushort)42);
            w.Write((uint)ifd0Offset);

            // IFD0: ExifIFDPointer
            w.Write((ushort)1);
            WriteEntry(w, tag: 0x8769, type: 4 /* LONG */, count: 1, value: exifIfdOffset);
            w.Write((uint)0); // 次の IFD なし

            // Exif IFD: DateTimeOriginal（4バイトに収まらないので値はオフセット参照）
            w.Write((ushort)1);
            WriteEntry(w, tag: 0x9003, type: 2 /* ASCII */, count: (uint)dateBytes.Length, value: dataOffset);
            w.Write((uint)0);

            w.Write(dateBytes);
            w.Flush();
            return ms.ToArray();
        }

        private static void WriteEntry(BinaryWriter w, ushort tag, ushort type, uint count, uint value)
        {
            w.Write(tag);
            w.Write(type);
            w.Write(count);
            w.Write(value);
        }

        /// <summary>長さ(ビッグエンディアン) + 種別 + データ + CRC32(種別+データ) の PNG チャンクを作る。</summary>
        private static byte[] BuildChunk(string type, byte[] data)
        {
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            var chunk = new byte[4 + 4 + data.Length + 4];

            WriteUInt32BigEndian(chunk, 0, (uint)data.Length);
            Buffer.BlockCopy(typeBytes, 0, chunk, 4, 4);
            Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
            WriteUInt32BigEndian(chunk, 8 + data.Length, Crc32(chunk, 4, 4 + data.Length));
            return chunk;
        }

        private static void WriteUInt32BigEndian(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        private static readonly uint[] CrcTable = CreateCrcTable();

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>PNG 仕様の CRC-32（ISO 3309 / ITU-T V.42 と同じ多項式）。</summary>
        public static uint Crc32(byte[] buf, int offset, int length)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = offset; i < offset + length; i++)
                c = CrcTable[(c ^ buf[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
