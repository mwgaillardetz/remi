using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Remi.Desktop.Services;

public interface IApiKeyStore
{
    string? Read();
    void Write(string? key);
}

public sealed class ProtectedApiKeyStore(string path) : IApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Remi.OpenAI.v1");
    public string? Read()
    {
        if (!File.Exists(path)) return null;
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public void Write(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = Encoding.UTF8.GetBytes(key);
        try
        {
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path + ".tmp", encrypted); File.Move(path + ".tmp", path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

internal sealed class MemoryApiKeyStore : IApiKeyStore
{
    private string? value;
    public string? Read() => value;
    public void Write(string? key) => value = key;
}
