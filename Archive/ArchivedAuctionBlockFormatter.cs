using MessagePack;
using MessagePack.Formatters;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// MessagePack formatter of <see cref="ArchivedAuctionBlock"/>: the same array layout the attribute based (dynamic) formatter
    /// writes for <c>[Key(0..37)]</c>, except that the array only grows past 38 elements when a later key is not null.
    /// The dynamic formatter always writes max key + 1 elements, so adding key 38 would have changed the bytes, and with them
    /// the <see cref="ArchivedAuctionBlock.Sha256"/> stored for every block already archived. Unknown trailing elements are skipped on read.
    /// </summary>
    public sealed class ArchivedAuctionBlockFormatter : IMessagePackFormatter<ArchivedAuctionBlock>
    {
        /// <summary>Elements written by the format before key 38 existed (keys 0-37).</summary>
        public const int BaseKeyCount = 38;

        /// <inheritdoc/>
        public void Serialize(ref MessagePackWriter writer, ArchivedAuctionBlock value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            var count = value.UnknownEnchantments != null ? BaseKeyCount + 1 : BaseKeyCount;
            writer.WriteArrayHeader(count);
            writer.Write(value.FormatVersion);
            writer.Write(value.Tag);
            writer.Write(value.Month);
            writer.Write(value.Part);
            writer.Write(value.Source);
            writer.Write(value.FirstEndTicks);
            W(ref writer, value.EndDeltasMs, options);
            W(ref writer, value.StartOffsetMs, options);
            W(ref writer, value.Uuid, options);
            W(ref writer, value.AuctionUid, options);
            W(ref writer, value.IsSold, options);
            W(ref writer, value.Bin, options);
            W(ref writer, value.Claimed, options);
            W(ref writer, value.Auctioneer, options);
            W(ref writer, value.ProfileId, options);
            W(ref writer, value.HighestBidder, options);
            W(ref writer, value.HighestBidAmount, options);
            W(ref writer, value.StartingBid, options);
            W(ref writer, value.Count, options);
            W(ref writer, value.Tier, options);
            W(ref writer, value.Category, options);
            W(ref writer, value.Reforge, options);
            W(ref writer, value.AnvilUses, options);
            W(ref writer, value.ItemName, options);
            W(ref writer, value.ItemCreatedAtTicks, options);
            W(ref writer, value.ItemBytes, options);
            W(ref writer, value.SerialisedBids, options);
            W(ref writer, value.Coop, options);
            W(ref writer, value.ClaimedBidders, options);
            W(ref writer, value.NbtLookup, options);
            W(ref writer, value.Enchantments, options);
            W(ref writer, value.HighestBidderName, options);
            W(ref writer, value.ProfileName, options);
            W(ref writer, value.CoopName, options);
            W(ref writer, value.Color, options);
            W(ref writer, value.ItemUid, options);
            W(ref writer, value.ItemId, options);
            W(ref writer, value.NbtLookupStrings, options);
            if (count > BaseKeyCount)
                W(ref writer, value.UnknownEnchantments, options);
        }

        /// <inheritdoc/>
        public ArchivedAuctionBlock Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            options.Security.DepthStep(ref reader);
            try
            {
                var count = reader.ReadArrayHeader();
                var b = new ArchivedAuctionBlock();
                for (var key = 0; key < count; key++)
                {
                    switch (key)
                    {
                        case 0: b.FormatVersion = reader.ReadInt32(); break;
                        case 1: b.Tag = reader.ReadString(); break;
                        case 2: b.Month = reader.ReadInt32(); break;
                        case 3: b.Part = reader.ReadInt32(); break;
                        case 4: b.Source = reader.ReadString(); break;
                        case 5: b.FirstEndTicks = reader.ReadInt64(); break;
                        case 6: b.EndDeltasMs = R<long[]>(ref reader, options); break;
                        case 7: b.StartOffsetMs = R<long[]>(ref reader, options); break;
                        case 8: b.Uuid = R<byte[][]>(ref reader, options); break;
                        case 9: b.AuctionUid = R<long[]>(ref reader, options); break;
                        case 10: b.IsSold = R<bool[]>(ref reader, options); break;
                        case 11: b.Bin = R<bool[]>(ref reader, options); break;
                        case 12: b.Claimed = R<bool[]>(ref reader, options); break;
                        case 13: b.Auctioneer = R<byte[][]>(ref reader, options); break;
                        case 14: b.ProfileId = R<byte[][]>(ref reader, options); break;
                        case 15: b.HighestBidder = R<byte[][]>(ref reader, options); break;
                        case 16: b.HighestBidAmount = R<long[]>(ref reader, options); break;
                        case 17: b.StartingBid = R<long[]>(ref reader, options); break;
                        case 18: b.Count = R<int[]>(ref reader, options); break;
                        case 19: b.Tier = R<byte[]>(ref reader, options); break;
                        case 20: b.Category = R<byte[]>(ref reader, options); break;
                        case 21: b.Reforge = R<byte[]>(ref reader, options); break;
                        case 22: b.AnvilUses = R<short[]>(ref reader, options); break;
                        case 23: b.ItemName = R<string[]>(ref reader, options); break;
                        case 24: b.ItemCreatedAtTicks = R<long[]>(ref reader, options); break;
                        case 25: b.ItemBytes = R<byte[][]>(ref reader, options); break;
                        case 26: b.SerialisedBids = R<byte[][]>(ref reader, options); break;
                        case 27: b.Coop = R<byte[][]>(ref reader, options); break;
                        case 28: b.ClaimedBidders = R<byte[][]>(ref reader, options); break;
                        case 29: b.NbtLookup = R<byte[][]>(ref reader, options); break;
                        case 30: b.Enchantments = R<byte[][]>(ref reader, options); break;
                        case 31: b.HighestBidderName = R<string[]>(ref reader, options); break;
                        case 32: b.ProfileName = R<string[]>(ref reader, options); break;
                        case 33: b.CoopName = R<string[]>(ref reader, options); break;
                        case 34: b.Color = R<int?[]>(ref reader, options); break;
                        case 35: b.ItemUid = R<long[]>(ref reader, options); break;
                        case 36: b.ItemId = R<byte[][]>(ref reader, options); break;
                        case 37: b.NbtLookupStrings = R<byte[][]>(ref reader, options); break;
                        case 38: b.UnknownEnchantments = R<byte[][]>(ref reader, options); break;
                        default: reader.Skip(); break;
                    }
                }
                return b;
            }
            finally
            {
                reader.Depth--;
            }
        }

        private static void W<T>(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
            => options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value, options);

        private static T R<T>(ref MessagePackReader reader, MessagePackSerializerOptions options)
            => options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
    }
}
