using BGV.AuthAPI.Models.Responses;
using BGV.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BGV.AuthAPI.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public TokenService(IConfiguration configuration, IDistributedCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<TokenPair> GenerateTokensAsync(string userId)
    {
        var accessToken = GenerateJwtToken(userId);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token in Redis with userId
        await _cache.SetStringAsync($"refresh:{refreshToken}", userId, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        });

        return new TokenPair { AccessToken = accessToken, RefreshToken = refreshToken };
    }

    public async Task<Result<TokenPair>> RefreshTokenAsync(string refreshToken)
    {
        var userId = await _cache.GetStringAsync($"refresh:{refreshToken}");
        if (string.IsNullOrEmpty(userId))
            return Result<TokenPair>.Fail("Invalid refresh token");

        // Revoke old refresh token
        await _cache.RemoveAsync($"refresh:{refreshToken}");

        // Generate new tokens
        var newTokens = await GenerateTokensAsync(userId);
        return Result<TokenPair>.Ok(newTokens);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        await _cache.RemoveAsync($"refresh:{refreshToken}");
    }

    private string GenerateJwtToken(string userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}