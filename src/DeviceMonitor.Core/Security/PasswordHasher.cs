using System.Security.Cryptography;

namespace DeviceMonitor.Core.Security;

/// <summary>
/// 密码哈希服务：PBKDF2 + 随机盐，绝不存明文。
/// </summary>
/// <remarks>
/// 为什么用 PBKDF2 而不是 MD5/SHA？
/// - MD5/SHA 是"快哈希"，抗暴力破解弱；PBKDF2 靠大量迭代故意拖慢，
///   让离线穷举成本高几个数量级。
/// - 每个用户独立随机盐，即使两人密码相同，哈希也不同（防彩虹表）。
///
/// 存储格式：PasswordHash 与 Salt 均为 Base64 字符串。
/// </remarks>
public static class PasswordHasher
{
    /// <summary>迭代次数（越高越安全但越慢；10 万次是当前工业界常用值）。</summary>
    private const int Iterations = 100_000;

    /// <summary>盐长度（字节）。</summary>
    private const int SaltSize = 16;

    /// <summary>哈希输出长度（字节），32 字节 = 256 位。</summary>
    private const int HashSize = 32;

    /// <summary>
    /// 计算密码哈希，返回 (哈希Base64, 盐Base64)。
    /// </summary>
    public static (string Hash, string Salt) Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("密码不能为空。", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// 校验密码：用同样的盐和参数重算哈希，与存储值比对（常数时间比较）。
    /// </summary>
    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(storedHash) ||
            string.IsNullOrEmpty(storedSalt))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(storedSalt);
            expected = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;  // 存储值损坏
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
