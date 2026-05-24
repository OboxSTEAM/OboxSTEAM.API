using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public AuthController(IAuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        /// <summary>
        /// Register a new user account.
        /// </summary>
        /// <param name="userDto">User registration data.</param>
        /// <returns>Registered user information.</returns>
        [HttpPost("register")]
        [SwaggerOperation(
            Summary = "Register a new user",
            Description = "Creates a new user account with the provided registration information."
        )]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 201)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto userDto)
        {
            var result = await _authService.RegisterUserAsync(userDto);
            return StatusCode(201, ApiResult<UserDto>.Success(result!, "201", "Registered successfully."));
        }

        /// <summary>
        /// User login.
        /// </summary>
        /// <param name="dto">Login credentials.</param>
        /// <returns>JWT access and refresh tokens.</returns>
        [HttpPost("login")]
        [SwaggerOperation(
            Summary = "User login",
            Description = "Authenticate user and return JWT tokens."
        )]
        [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 400)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var result = await _authService.LoginAsync(dto, _configuration);
            return Ok(ApiResult<LoginResponseDto>.Success(result!, "200", "Login successful."));
        }

        /// <summary>
        /// Logout the current user.
        /// </summary>
        /// <returns>Logout result.</returns>
        [HttpPost("logout")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Logout user",
            Description = "Logs out the currently authenticated user."
        )]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [ProducesResponseType(typeof(ApiResult<object>), 500)]
        public async Task<IActionResult> Logout()
        {
            var result = await _authService.LogoutAsync();
            return Ok(ApiResult<object>.Success(result!, "200", "Logged out successfully."));
        }

        /// <summary>
        /// Request to reset password by sending a link to user's email.
        /// </summary>
        /// <param name="dto">Forgot password request data.</param>
        /// <returns>Result of the request.</returns>
        [HttpPost("send-resetlink")]
        [SwaggerOperation(
            Summary = "Forgot password request",
            Description = "Sends a password reset link to the specified email if it exists."
        )]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            var success = await _authService.ForgotPasswordAsync(dto.Email);
            if (!success) return BadRequest(ApiResult.Failure("400", "Failed to send reset password link."));
            return Ok(ApiResult.Success("200", "Password reset link has been sent to your email."));
        }

        /// <summary>
        /// Reset user password using token sent to email.
        /// </summary>
        /// <param name="dto">Reset password data.</param>
        /// <returns>Password reset result.</returns>
        [HttpPost("forgot-password")]
        [SwaggerOperation(
            Summary = "Reset password",
            Description = "Reset user password using token sent to email."
        )]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var reset = await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
            if (!reset) return BadRequest(ApiResult.Failure("400", "Token is invalid, expired or data is invalid."));
            return Ok(ApiResult.Success("200", "Password was reset successfully."));
        }

        /// <summary>
        /// Refresh JWT access token using a valid refresh token.
        /// </summary>
        /// <param name="requestToken">Refresh token data.</param>
        /// <returns>New JWT tokens.</returns>
        [HttpPost("refresh-token")]
        [SwaggerOperation(
            Summary = "Refresh JWT token",
            Description = "Refresh JWT access token using a valid refresh token."
        )]
        [ProducesResponseType(typeof(ApiResult<LoginResponseDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [ProducesResponseType(typeof(ApiResult<object>), 401)]
        [ProducesResponseType(typeof(ApiResult<object>), 500)]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshRequestDto requestToken)
        {
            var result = await _authService.RefreshTokenAsync(requestToken, _configuration);
            return Ok(ApiResult<object>.Success(result!, "200", "Refresh Token successfully"));
        }

        /// <summary>
        /// Resend OTP to user's email for registration or password reset.
        /// </summary>
        /// <param name="dto">Resend OTP request data.</param>
        /// <returns>OTP resend result.</returns>
        [HttpPost("resend-otp")]
        [SwaggerOperation(
            Summary = "Resend OTP",
            Description = "Resend OTP to user's email for registration or password reset."
        )]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        [ProducesResponseType(typeof(ApiResult<object>), 500)]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequestDto dto)
        {
            var success = await _authService.ResendOtpAsync(dto.Email, dto.Purpose);
            if (!success)
            {
                return BadRequest(ApiResult.Failure("400", "Failed to resend OTP. Possibly invalid state or email."));
            }

            return Ok(ApiResult.Success("200", "OTP resent successfully."));
        }

        /// <summary>
        /// Verify OTP for email confirmation.
        /// </summary>
        /// <param name="dto">OTP verification data.</param>
        /// <returns>Verification result.</returns>
        [HttpPost("verify-otp")]
        [SwaggerOperation(
            Summary = "Verify OTP",
            Description = "Verify OTP for email confirmation and activate account."
        )]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var verified = await _authService.VerifyEmailOtpAsync(dto.Email, dto.Otp);
            if (!verified)
                return BadRequest(ApiResult.Failure("400", "OTP is invalid or expired."));
            return Ok(ApiResult.Success("200", "Verification successful. Account activated."));
        }
    }
}