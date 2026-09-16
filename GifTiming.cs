using System;
using System.IO;
using System.Text;

namespace ForTheEmperor;

// WPF's GIF writer drops timing/loop metadata on some Windows codecs. Add standard
// GIF89a control blocks without changing the rendered or compressed frame pixels.
internal static class GifTiming
{
    internal static byte[] Set(byte[] source, ushort delay)
    {
        using var output = new MemoryStream();
        int cursor = 13 + ((source[10] & 0x80) != 0 ? 3 * (1 << ((source[10] & 7) + 1)) : 0);
        output.Write(source, 0, cursor);
        output.Position = 0; output.Write(Encoding.ASCII.GetBytes("GIF89a")); output.Position = output.Length;
        output.Write(new byte[] { 0x21, 0xFF, 11 }); output.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0")); output.Write(new byte[] { 3, 1, 0, 0, 0 });
        void Blocks() { while (cursor < source.Length) { int length = source[cursor++]; cursor += length; if (length == 0) return; } throw new InvalidDataException("Truncated GIF"); }
        while (cursor < source.Length)
        {
            int begin = cursor; byte kind = source[cursor++];
            if (kind == 0x3B) { output.WriteByte(kind); break; }
            if (kind == 0x21)
            {
                byte type = source[cursor++]; Blocks();
                if (type is not 0xF9 and not 0xFF) output.Write(source, begin, cursor - begin);
            }
            else if (kind == 0x2C)
            {
                output.Write(new byte[] { 0x21, 0xF9, 4, 4, (byte)delay, (byte)(delay >> 8), 0, 0 });
                byte flags = source[cursor + 8]; cursor += 9;
                if ((flags & 0x80) != 0) cursor += 3 * (1 << ((flags & 7) + 1));
                cursor++; Blocks(); output.Write(source, begin, cursor - begin);
            }
            else throw new InvalidDataException("Invalid GIF block");
        }
        return output.ToArray();
    }
}
