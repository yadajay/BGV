using BGV.AuthAPI.Models.Responses;
using BGV.Core.Models;

namespace BGV.AuthAPI.Services;

public interface IMfaService
{
    Task<Result<EnableMfaResponse>> EnableMfaAsync(string userId);
    Task<Result> VerifyMfaAsync(string userId, string code);
}