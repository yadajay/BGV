using RCD.AuthAPI.Models.Responses;
using RCD.Core.Models;

namespace RCD.AuthAPI.Services;

public interface ITokenService
{
    Task<TokenPair> GenerateTokensAsync(string userId);
    Task<Result<TokenPair>> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
}