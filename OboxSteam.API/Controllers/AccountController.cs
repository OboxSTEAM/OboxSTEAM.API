using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.UserDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers
{
    [Route("api/account")]
    [ApiController]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        /// <summary>
        /// Get current authenticated user profile.
        /// </summary>
        /// <returns>User profile information.</returns>
        [HttpGet("me")]
        [SwaggerOperation(
            Summary = "Get current user profile",
            Description = "Retrieves the profile information of the currently authenticated user."
        )]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 401)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await _accountService.GetCurrentUserAsync();
            return Ok(ApiResult<UserDto>.Success(result!, "200", "User profile retrieved successfully."));
        }

        /// <summary>
        /// Get user profile by user ID.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>User profile information.</returns>
        [HttpGet("{userId:guid}")]
        [SwaggerOperation(
            Summary = "Get user profile by ID",
            Description = "Retrieves the profile information of a specific user by their ID."
        )]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> GetUserById([FromRoute] Guid userId)
        {
            var result = await _accountService.GetUserByIdAsync(userId);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "User profile retrieved successfully."));
        }

        /// <summary>
        /// Update current user profile.
        /// </summary>
        /// <param name="updateUserDto">User profile update data.</param>
        /// <returns>Updated user profile information.</returns>
        [HttpPut("me")]
        [SwaggerOperation(
            Summary = "Update user profile",
            Description = "Updates the profile information of the currently authenticated user."
        )]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 401)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateUserDto updateUserDto)
        {
            var result = await _accountService.UpdateUserProfileAsync(updateUserDto);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "Profile updated successfully."));
        }

        /// <summary>
        /// Upload avatar for the current authenticated user.
        /// </summary>
        /// <param name="file">Image file (jpg, jpeg, png, gif). Max 5 MB.</param>
        /// <returns>Updated user profile with new avatar URL.</returns>
        [HttpPost("me/avatar")]
        [SwaggerOperation(
            Summary = "Upload user avatar",
            Description = "Uploads a new avatar image for the currently authenticated user. Replaces the existing avatar if one exists."
        )]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 400)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 401)]
        [ProducesResponseType(typeof(ApiResult<UserDto>), 404)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var result = await _accountService.UploadAvatarAsync(file);
            return Ok(ApiResult<UserDto>.Success(result!, "200", "Avatar uploaded successfully."));
        }
    }
}
