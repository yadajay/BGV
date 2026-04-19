using RCD.AuthAPI.Models.Responses;
using RCD.Core.Models;

namespace RCD.AuthAPI.Services;

public interface IMfaService
{
    Task<Result<EnableMfaResponse>> EnableMfaAsync(string userId);
    Task<Result> VerifyMfaAsync(string userId, string code);
}