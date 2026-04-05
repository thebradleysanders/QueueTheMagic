using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace XlightsQueue.Services;

public class AdminAuthService(IConfiguration configuration) {
    private readonly string _password = configuration["AdminPassword"] ?? "changeme";
    private readonly string _jwtSecret = configuration["Jwt:Secret"] ?? "qtm-default-secret-key-change-this-in-production";
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "QueueTheMagic";

    public bool ValidatePassword(string password) =>
        !string.IsNullOrEmpty(password) && password == _password;

    public string GenerateToken() {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: [new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
