using System.Security.Cryptography;
using System.Text;

namespace FunctionApp.Infrastructure;

public interface IHashService
{
    string Sha256Hex(string text);
}

public sealed class Sha256HashService : IHashService
{
    public string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
