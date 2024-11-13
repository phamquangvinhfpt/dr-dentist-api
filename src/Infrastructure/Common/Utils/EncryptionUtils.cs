using System.Security.Cryptography;
using System.Text;

namespace FSH.WebApi.Infrastructure.Common.Utils;
public class EncryptionUtils
{
    private const int AESBLOCKSIZE = 16;
    private const string Password = "DCMS2024";

    private static void DeriveKey(string password, out byte[] key)
    {
        using var sha256 = SHA256.Create();
        key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    private static void GenerateIV(out byte[] iv)
    {
        using var rng = new RNGCryptoServiceProvider();
        iv = new byte[AESBLOCKSIZE];
        rng.GetBytes(iv);
    }

    private static string Base64Encode(byte[] buffer)
    {
        return Convert.ToBase64String(buffer);
    }

    private static byte[] Base64Decode(string base64String)
    {
        return Convert.FromBase64String(base64String);
    }

    public static byte[] Encrypt(string plaintext, byte[] key, byte[] iv)
    {
        using var aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plaintext);
        }

        return msEncrypt.ToArray();
    }

    public static string Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

        using var msDecrypt = new MemoryStream(ciphertext);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        return srDecrypt.ReadToEnd();
    }

    public static string SimpleEnc(string input)
    {
        DeriveKey(Password, out byte[] key);
        GenerateIV(out byte[] iv);

        byte[] ciphertext = Encrypt(input, key, iv);

        byte[] combined = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, iv.Length, ciphertext.Length);

        return Base64Encode(combined);
    }

    public static string SimpleDec(string input)
    {
        DeriveKey(Password, out byte[] key);

        byte[] decodedInput = Base64Decode(input);

        byte[] iv = new byte[AESBLOCKSIZE];
        Buffer.BlockCopy(decodedInput, 0, iv, 0, iv.Length);

        byte[] ciphertext = new byte[decodedInput.Length - iv.Length];
        Buffer.BlockCopy(decodedInput, iv.Length, ciphertext, 0, ciphertext.Length);

        return Decrypt(ciphertext, key, iv);
    }
}
