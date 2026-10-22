using System;
using System.Text;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Lossless compact encoding of the 32-char hex ids (auction uuid, player and profile uuids) used in archive blocks.
    /// A lowercase 32-char hex string becomes its 16 raw bytes. Anything else (upper case, dashes, "unknown", empty)
    /// is kept verbatim as <c>0xFF, pad, utf8..., [0x00 if pad]</c>, which never has length 16, so decoding is unambiguous.
    /// <c>null</c> stays <c>null</c>.
    /// </summary>
    public static class ArchiveIds
    {
        private const byte RawMarker = 0xFF;

        /// <summary>Encode an id string.</summary>
        public static byte[] Encode(string id)
        {
            if (id == null)
                return null;
            if (IsCanonical(id))
                return Convert.FromHexString(id);
            var utf8 = Encoding.UTF8.GetBytes(id);
            var pad = 2 + utf8.Length == 16 ? 1 : 0;
            var result = new byte[2 + utf8.Length + pad];
            result[0] = RawMarker;
            result[1] = (byte)pad;
            utf8.CopyTo(result, 2);
            return result;
        }

        /// <summary>Decode bytes produced by <see cref="Encode"/>.</summary>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null)
                return null;
            if (bytes.Length == 16)
                return Convert.ToHexString(bytes).ToLowerInvariant();
            if (bytes.Length < 2 || bytes[0] != RawMarker)
                throw new FormatException("Invalid archived id encoding of length " + bytes.Length);
            var pad = bytes[1];
            return Encoding.UTF8.GetString(bytes, 2, bytes.Length - 2 - pad);
        }

        private static bool IsCanonical(string id)
        {
            if (id.Length != 32)
                return false;
            foreach (var c in id)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                    return false;
            return true;
        }
    }
}
