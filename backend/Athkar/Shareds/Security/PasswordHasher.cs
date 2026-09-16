using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Athkar.Shareds.Security;

/// <summary>
/// Argon2id, with the salt and the parameters stored beside the hash so a future
/// change to the cost can be rolled out without invalidating existing logins.
///
/// Format: <c>$argon2id$v=19$m=..,t=..,p=..$salt$hash</c>, base64 for both
/// binary fields — the PHC string layout, so the stored value is recognisable to
/// anyone who has seen one before.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemoryKb = 19 * 1024;
    private const int Iterations = 2;
    private const int Parallelism = 1;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, MemoryKb, Iterations, Parallelism);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        // A malformed or empty stored hash is a "no", never an exception: this
        // runs on the login path, where a thrown exception is a 500 telling an
        // anonymous caller that this particular account is unusual.
        var parts = hash?.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 5 } || parts[0] != "argon2id") return false;

        var parameters = parts[2].Split(',')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => int.TryParse(p[1], out var v) ? v : 0);

        if (!parameters.TryGetValue("m", out var memory) ||
            !parameters.TryGetValue("t", out var iterations) ||
            !parameters.TryGetValue("p", out var parallelism))
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, memory, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt,
        int memoryKb, int iterations, int parallelism, int size = HashSize)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(size);
    }
}
