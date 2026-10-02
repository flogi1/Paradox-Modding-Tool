using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ParadoxModTool.SaveGames;

/// <summary>
/// Inhalt eines komprimierten Text-Stands (Crusader Kings III, Kopfzeile <c>SAV…02</c>): hinter Kopfzeile und <c>meta_data</c> liegt ein ZIP, dessen
/// Offsets ab Archivbeginn zählen, sodass <see cref="ZipArchive"/> über die ganze Datei keine Einträge findet. Gelesen wird deshalb der erste lokale
/// Dateikopf direkt: Eintrag <c>gamestate</c>, Verfahren 8 (deflate) oder 0 (gespeichert). Nichts wird auf die Platte geschrieben.
/// </summary>
public static class SaveGamestateStream
{
    private const int LocalHeaderLength = 30;

    private static readonly byte[] LocalHeaderSignature = { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>Stream über den entpackten Eintrag <c>gamestate</c>; <paramref name="file"/> bleibt offen. Wirft <see cref="InvalidDataException"/>.</summary>
    public static Stream Open(Stream file)
    {
        try
        {
            var start = FindLocalHeader(file);
            if (start < 0)
            {
                throw Unreadable();
            }

            file.Position = start;
            var header = new byte[LocalHeaderLength];
            file.ReadExactly(header);
            var flags = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
            var method = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8));
            var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(18));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
            var name = new byte[nameLength];
            file.ReadExactly(name);
            file.Position += extraLength;

            // Bit 0: verschlüsselt; bei Verfahren 0 muss die Größe im Kopf stehen (Bit 3 = Größe erst dahinter).
            var encrypted = (flags & 1) != 0;
            if (encrypted || Encoding.ASCII.GetString(name) != "gamestate")
            {
                throw Unreadable();
            }

            return method switch
            {
                8 => new DeflateStream(file, CompressionMode.Decompress, leaveOpen: true),
                0 when (flags & 8) == 0 => new BoundedStream(file, compressedSize),
                _ => throw Unreadable(),
            };
        }
        catch (EndOfStreamException)
        {
            throw Unreadable();
        }
    }

    internal static InvalidDataException Unreadable() => new(SaveExplorerTexts.CompressedUnreadable);

    private static long FindLocalHeader(Stream file)
    {
        file.Position = 0;
        var buffer = new byte[64 * 1024];
        long offset = 0;
        var carry = 0;
        while (true)
        {
            var count = file.Read(buffer, carry, buffer.Length - carry);
            if (count <= 0)
            {
                return -1;
            }

            var length = carry + count;
            var index = buffer.AsSpan(0, length).IndexOf(LocalHeaderSignature);
            if (index >= 0)
            {
                return offset + index;
            }

            // Die letzten drei Bytes könnten der Anfang der Signatur sein.
            carry = Math.Min(LocalHeaderSignature.Length - 1, length);
            Array.Copy(buffer, length - carry, buffer, 0, carry);
            offset += length - carry;
        }
    }

    /// <summary>Liest höchstens <c>length</c> Bytes ab der aktuellen Position (gespeicherter, nicht komprimierter Eintrag).</summary>
    private sealed class BoundedStream(Stream inner, long length) : Stream
    {
        private long _remaining = length;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0)
            {
                return 0;
            }

            var read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
