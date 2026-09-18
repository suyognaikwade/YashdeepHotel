using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.DTOs;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Cloud;

namespace Yashdeep.Server.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly CloudDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;

    public AuthController(
        CloudDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Invalid username or password." });

        bool isValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password);
        if (!isValid)
            return Unauthorized(new { message = "Invalid username or password." });

        var tokenResult = _tokenService.GenerateTokens(user);
        var tokenHash = _tokenService.HashRefreshToken(tokenResult.RefreshToken);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var refreshTokenEntity = new RefreshToken(
            Guid.NewGuid(),
            user.UserId,
            user.TenantId,
            tokenHash,
            tokenResult.RefreshTokenExpiresAtUtc,
            ipAddress);

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponseDto
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken,
            AccessTokenExpiresAtUtc = tokenResult.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = tokenResult.RefreshTokenExpiresAtUtc,
            User = new UserProfileDto
            {
                UserId = user.UserId,
                TenantId = user.TenantId,
                OrganizationId = user.OrganizationId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Roles = user.Roles.Select(r => r.Name).ToList()
            }
        };

        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { message = "Refresh token is required." });

        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken == null || !existingToken.IsActive)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        existingToken.Revoke();

        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserId == existingToken.UserId, cancellationToken);

        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "User is inactive or no longer exists." });

        var newTokenResult = _tokenService.GenerateTokens(user);
        var newTokenHash = _tokenService.HashRefreshToken(newTokenResult.RefreshToken);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var newRefreshTokenEntity = new RefreshToken(
            Guid.NewGuid(),
            user.UserId,
            user.TenantId,
            newTokenHash,
            newTokenResult.RefreshTokenExpiresAtUtc,
            ipAddress);

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponseDto
        {
            AccessToken = newTokenResult.AccessToken,
            RefreshToken = newTokenResult.RefreshToken,
            AccessTokenExpiresAtUtc = newTokenResult.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = newTokenResult.RefreshTokenExpiresAtUtc,
            User = new UserProfileDto
            {
                UserId = user.UserId,
                TenantId = user.TenantId,
                OrganizationId = user.OrganizationId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Roles = user.Roles.Select(r => r.Name).ToList()
            }
        };

        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        if (request != null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
            var existingToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

            if (existingToken != null)
            {
                existingToken.Revoke();
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
            return Unauthorized(new { message = "Tenant context not authenticated." });

        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == _tenantContext.UserId, cancellationToken);

        if (user == null)
            return NotFound(new { message = "User not found in tenant context." });

        var userProfile = new UserProfileDto
        {
            UserId = user.UserId,
            TenantId = user.TenantId,
            OrganizationId = user.OrganizationId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Roles = user.Roles.Select(r => r.Name).ToList()
        };

        return Ok(userProfile);
    }
}
