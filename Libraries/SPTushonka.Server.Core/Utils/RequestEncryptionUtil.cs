using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.ObjectPool;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.Utils;

[Injectable]
public class RequestEncryptionUtil(ISptLogger<RequestEncryptionUtil> logger)
{
    private readonly byte[] _key = "7*YabV3MfOfyE*lhI*l*Qx*q"u8.ToArray();
    private readonly ObjectPool<byte[]> _objectPool = new DefaultObjectPool<byte[]>(new PooledObjectPolicy());

    private static readonly Lock _lock = new();
    private static readonly int[] _paddingTable = ComputeArray(6, 11);
    private static int _rngTableLength = XtimeCompute();

    public bool IsAesEncrypted(IHeaderDictionary headers)
    {
        if (headers.TryGetValue("X-Encryption", out var header))
        {
            return header.ToString() == "AES";
        }

        return false;
    }

    public async Task<byte[]> DecryptAsync(byte[] data)
    {
        var iv = _objectPool.Get();
        var cipherBytesLength = data.Length - iv.Length;

        var cipherText = ArrayPool<byte>.Shared.Rent(cipherBytesLength);

        Array.Copy(data, iv.Length, cipherText, 0, cipherBytesLength);
        Array.Copy(data, iv, iv.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        var cryptoTransform = aes.CreateDecryptor(aes.Key, aes.IV);
        var tmpArray = ArrayPool<byte>.Shared.Rent(cipherBytesLength);

        byte[] result = null!;
        try
        {
            using var memoryStream = new MemoryStream(tmpArray);
            await using var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write);

            cryptoStream.Write(cipherText, 0, cipherBytesLength);
            cryptoStream.Close();

            result = memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            logger.Error("Failed to decrypt aes data", ex);
        }

        ArrayPool<byte>.Shared.Return(cipherText);
        ArrayPool<byte>.Shared.Return(tmpArray);
        return result;
    }

    public async Task<Stream> DeShuffleAsync(Stream stream, bool shouldShuffle = true, CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        // /client/metadata is not shuffled on the way in, and nothing under 4 bytes is shuffled at all
        var length = (int)buffer.Length;
        if (!shouldShuffle || length < 4)
        {
            buffer.Position = 0;

            return buffer;
        }

        var frame = buffer.GetBuffer().AsSpan(0, length);
        var startEntry = length % 0xAAB;

        for (var offset = 1; offset < length; offset++)
        {
            var swap = (int)(Entry(offset + startEntry) % (uint)offset);
            (frame[offset], frame[swap]) = (frame[swap], frame[offset]);
        }

        var size = BinaryPrimitives.ReadInt32LittleEndian(frame);
        if (size < 0 || size > length - 4)
        {
            throw new InvalidOperationException($"Deshuffled body carries an impossible length ({size})");
        }

        return new MemoryStream(buffer.GetBuffer(), 4, size, false);
    }

    private class PooledObjectPolicy : IPooledObjectPolicy<byte[]>
    {
        public byte[] Create()
        {
            return new byte[16];
        }

        public bool Return(byte[] obj)
        {
            Array.Clear(obj, 0, 16);
            return true;
        }
    }

    /// <summary>How many bytes of padding the next response carries. Live rolls this per response.</summary>
    public int NextPadding()
    {
        lock (_lock)
        {
            var rng = (_rngTableLength + 1) % 0x25A18;
            _rngTableLength = rng;

            return _paddingTable[(int)(Entry(rng) % (uint)_paddingTable.Length)];
        }
    }

    /// <summary>
    ///     Shuffle a response frame in place. The frame is [4 byte length][payload][padding] with the
    ///     payload already at offset 4, this writes the header and padding, then permutes.
    /// </summary>
    public void ShuffleInPlace(Span<byte> frame, int payloadLength)
    {
        BinaryPrimitives.WriteInt32LittleEndian(frame, payloadLength);

        // Live pads with the payload repeated from its start, the client drops it by the header
        var payload = frame.Slice(4, payloadLength);
        var padding = frame.Slice(4 + payloadLength);
        if (payloadLength == 0)
        {
            padding.Clear();
        }

        while (padding.Length > 0 && payloadLength > 0)
        {
            var take = Math.Min(padding.Length, payloadLength);
            payload.Slice(0, take).CopyTo(padding);
            padding = padding.Slice(take);
        }

        var startEntry = frame.Length % 0xAAB;

        for (var offset = frame.Length - 1; offset > 0; offset--)
        {
            var swap = (int)(Entry(offset + startEntry) % (uint)offset);
            (frame[offset], frame[swap]) = (frame[swap], frame[offset]);
        }
    }

    private static uint Entry(int index)
    {
        return (uint)(((1624453ul * (0x65F6Dul + (ulong)index)) + 1023920427ul) % 0x8ED7A18Dul);
    }

    private static int[] ComputeArray(int startIndex, int count)
    {
        var result = new int[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = startIndex + i;
        }

        return result;
    }

    private static int XtimeCompute()
    {
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 10_000L;
        var seconds32 = ticks / 10_000_000;
        var cycles = ticks / 1_541_360_000_000L;
        var value = (seconds32 - (154_136L * cycles)) % 0x25A18;
        if (value < 0)
        {
            value += 0x25A18;
        }

        return (int)value;
    }
}
