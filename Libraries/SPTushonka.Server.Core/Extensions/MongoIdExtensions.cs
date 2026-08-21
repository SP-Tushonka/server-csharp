using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Extensions;

public static class MongoIdExtensions
{
    //Temporary, but necessary
    public static IEnumerable<MongoId> ToMongoIds(this IEnumerable<string> source)
    {
        return source.Select(s => (MongoId)s);
    }

    /// <summary>
    /// Determines whether the specified <see cref="MongoId"/> is a valid 24-character hexadecimal string,
    /// which is the standard format for MongoDB ObjectIds.
    /// </summary>
    /// <param name="mongoId">The <see cref="MongoId"/> to validate.</param>
    /// <returns><see langword="true"/> if the <paramref name="mongoId"/> is a valid MongoDB ObjectId; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidMongoId(this MongoId mongoId)
    {
        return !mongoId.IsEmpty;
    }

    /// <summary>
    /// Converts a string into a <see cref="MongoId"/> for checking if a value is an id before using it as one.
    /// Accepts exactly what <see cref="IsValidMongoId(string)"/> accepts, so an empty string fails rather than producing an empty id
    /// </summary>
    /// <param name="mongoId">The string to parse.</param>
    /// <param name="parsed">The parsed id, or <see langword="default"/> when parsing fails.</param>
    /// <returns><see langword="true"/> if <paramref name="mongoId"/> was parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseMongoId(this string? mongoId, out MongoId parsed)
    {
        if (mongoId is null || !mongoId.IsValidMongoId())
        {
            parsed = default;

            return false;
        }

        parsed = new MongoId(mongoId);

        return true;
    }

    /// <summary>
    /// Determines whether the specified string is a valid 24-character hexadecimal representation
    /// of a MongoDB ObjectId.
    /// </summary>
    /// <param name="mongoId">The string to validate as a MongoDB ObjectId.</param>
    /// <returns><see langword="true"/> if the <paramref name="mongoId"/> is a valid MongoDB ObjectId; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidMongoId(this string mongoId)
    {
        var span = mongoId.AsSpan();

        if (span.Length != 24)
        {
            return false;
        }

        for (var i = 0; i < 24; i++)
        {
            var c = span[i];
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
