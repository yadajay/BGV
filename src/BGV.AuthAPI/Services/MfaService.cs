// using BGV.AuthAPI.Models.Responses;
// using BGV.AuthAPI.Repositories;
// using BGV.Core.Models;
// using OtpNet;
// using QRCoder;
// using System.Text;

// namespace BGV.AuthAPI.Services;

// public class MfaService : IMfaService
// {
//     private readonly IUserRepository _userRepository;

//     public MfaService(IUserRepository userRepository)
//     {
//         _userRepository = userRepository;
//     }

//     public async Task<Result<EnableMfaResponse>> EnableMfaAsync(string userId)
//     {
//         var user = await _userRepository.GetUserByIdAsync(userId);
//         if (user == null)
//             return Result<EnableMfaResponse>.Failure("User not found");

//         var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
//         var totp = new Totp(Base32Encoding.ToBytes(secret));

//         // Generate QR code URL
//         var qrCodeUrl = $"otpauth://totp/BGV:{user.Email}?secret={secret}&issuer=BGV";

//         // TODO: Save secret to user

//         return Result<EnableMfaResponse>.Success(new EnableMfaResponse
//         {
//             Secret = secret,
//             QrCodeUrl = qrCodeUrl
//         });
//     }

//     public async Task<Result> VerifyMfaAsync(string userId, string code)
//     {
//         var user = await _userRepository.GetUserByIdAsync(userId);
//         if (user == null)
//             return Result.Failure("User not found");

//         // TODO: Get secret from user
//         var secret = "placeholder"; // Replace with actual secret from DB

//         var totp = new Totp(Base32Encoding.ToBytes(secret));
//         var isValid = totp.VerifyTotp(code, out _);

//         if (!isValid)
//             return Result.Failure("Invalid MFA code");

//         return Result.Success();
//     }
// }