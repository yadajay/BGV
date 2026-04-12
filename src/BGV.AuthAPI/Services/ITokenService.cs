using BGV.AuthAPI.Models.Responses;
using BGV.Core.Models;

namespace BGV.AuthAPI.Services;

public interface ITokenService
{
    Task<TokenPair> GenerateTokensAsync(string userId);
    Task<Result<TokenPair>> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
}