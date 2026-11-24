using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

/// <summary>
/// "Friends &amp; family" passes. The admin creates a pass per person; they sign in with its short code and
/// can then request songs for free, any time (even before the season opens), without paying.
///
/// Guest tokens are deliberately NOT JWTs: they use their own HMAC key and a custom header, so they can never
/// be accepted as an admin token. Revoking a pass (deleting it) cuts off its token immediately.
/// </summary>
public class GuestService(IDbContextFactory<AppDbContext> dbFactory, IConfiguration configuration) {
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(60);

    private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes(
        "qtm-guest:" + (configuration["Jwt:Secret"] ?? "qtm-default-secret-key-change-this-in-production")));

    public static string NormalizeCode(string? code) =>
        new((code ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public async Task<GuestPass> CreateAsync(string name) {
        await using var db = await dbFactory.CreateDbContextAsync();
        string code;
        do { code = RandomCode(); } while (await db.GuestPasses.AnyAsync(g => g.Code == code));

        var pass = new GuestPass { Name = name.Trim(), Code = code };
        db.GuestPasses.Add(pass);
        await db.SaveChangesAsync();
        return pass;
    }

    /// <summary>Exchanges a code for a signed token, or null if the code is wrong.</summary>
    public async Task<(string Token, GuestPass Pass)?> SignInAsync(string? code) {
        var normalized = NormalizeCode(code);
        if (normalized.Length < 4 || normalized.Length > 12) return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        var pass = await db.GuestPasses.FirstOrDefaultAsync(g => g.Code == normalized);
        if (pass == null) return null;

        pass.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (MakeToken(pass.Id), pass);
    }

    /// <summary>Returns the pass for a valid, unexpired token whose pass still exists, otherwise null.</summary>
    public async Task<GuestPass?> ValidateAsync(string? token) {
        var parts = (token ?? "").Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var id) || !long.TryParse(parts[1], out var exp)) return null;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

        var expected = Sign($"{parts[0]}.{parts[1]}");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuestPasses.FindAsync(id);
    }

    public async Task RecordRequestAsync(int passId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.GuestPasses.Where(g => g.Id == passId)
            .ExecuteUpdateAsync(u => u.SetProperty(g => g.RequestCount, g => g.RequestCount + 1).SetProperty(g => g.LastUsedAt, DateTime.UtcNow));
    }

    private string MakeToken(int id) {
        var payload = $"{id}.{DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds()}";
        return $"{payload}.{Sign(payload)}";
    }

    private string Sign(string payload) =>
        Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(payload))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string RandomCode() {
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
