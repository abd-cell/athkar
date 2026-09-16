using Athkar.Shareds.Attributes;

namespace Athkar.Shareds.Security;

[SingletonInjectable]
public interface IPasswordHasher
{
    /// <summary>Hashes a password, salt included in the returned string.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification against a stored hash.</summary>
    bool Verify(string password, string hash);
}
