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

    private static readonly Lock _lock = new Lock();
    private static ulong[] _table = [];
    private static ulong _tableCursor = 0x65F6D;
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

    public async Task<Stream> DeShuffleAsync(
        Stream stream,
        bool shouldShuffle = true,
        CancellationToken cancellationToken = default
    )
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var backup = buffer.ToArray();

        // Some responses dont need shuffling
        // /client/metadata doesn't need shuffling for the request but does for the response
        // Some responses are too short (2 bytes) those don't need shuffling either
        if (!shouldShuffle || backup.Length < 4)
        {
            return new MemoryStream(backup);
        }

        var startEntry = backup.Length % 0xAAB;
        BuildTable(startEntry + backup.Length);

        var decryptEnd = backup.Length;
        var offset = 1;

        while (offset < decryptEnd)
        {
            var swapIndex = _table[offset + startEntry] % (ulong)offset;

            var temp = backup[offset];
            backup[offset] = backup[swapIndex];
            backup[swapIndex] = temp;

            offset++;
        }

        var size = BinaryPrimitives.ReadInt32LittleEndian(backup);
        var result = new byte[size];

        Buffer.BlockCopy(backup, 4, result, 0, size);

        return new MemoryStream(result);
    }

    public byte[] Shuffle(byte[] input)
    {
        var padding = GetShufflePadding();
        var output = new byte[padding + input.Length + 4];

        BinaryPrimitives.WriteInt32LittleEndian(output, input.Length);

        //Add padding which is probably completely useless, but Nikita seems to think it looks cool in Fiddler
        //
        {
            var dataOffset = 4;
            var dataLeftLength = output.Length - 4;

            while (dataLeftLength > input.Length)
            {
                Buffer.BlockCopy(input, 0, output, dataOffset, input.Length);

                dataLeftLength -= input.Length;
                dataOffset += input.Length;
            }

            if (dataLeftLength > 0)
            {
                Buffer.BlockCopy(input, 0, output, dataOffset, dataLeftLength);
            }
        }

        var startEntry = output.Length % 0xAAB;
        BuildTable(startEntry + output.Length);

        const int decryptEnd = 0;
        var offset = output.Length - 1;

        while (offset > decryptEnd)
        {
            var swapIndex = _table[offset + startEntry] % (ulong)offset;

            var temp = output[offset];
            output[offset] = output[swapIndex];
            output[swapIndex] = temp;

            offset--;
        }

        return output;
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

    private static void BuildTable(int size)
    {
        lock (_lock)
        {
            if (_table.Length >= size)
            {
                return;
            }

            var table = new ulong[size];
            Array.Copy(_table, table, _table.Length);

            for (var i = _table.Length; i < size; i++)
            {
                var offset = ((1624453ul * _tableCursor++) + 1023920427) % 0x8ED7A18Dul;
                if (_tableCursor > 0xA53F260DDB0ul)
                {
                    _tableCursor = 0;
                }

                table[i] = offset;
            }

            _table = table;
        }
    }

    private static int GetShufflePadding()
    {
        lock (_lock)
        {
            var rng = (_rngTableLength + 1) % 0x25A18;
            _rngTableLength = rng;

            BuildTable(rng + 1);

            return _paddingTable[(int)(_table[rng] % (ulong)_paddingTable.Length)];
        }
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
        var v = ((seconds32 - (154_136L * cycles)) % 0x25A18);
        if (v < 0)
        {
            v += 0x25A18;
        }

        return (int)v;
    }
}
