using Yashdeep.Domain.Entities;
using Yashdeep.Infrastructure.Security;
using Xunit;
using FluentAssertions;

namespace Yashdeep.Tests.Unit.Security;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher;

    public Argon2idPasswordHasherTests()
    {
        _hasher = new Argon2idPasswordHasher();
    }

    private static User CreateDummyUser()
    {
        return new User(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "testuser",
            "testuser@example.com",
            "dummy_hash");
    }

    [Fact]
    public void HashPassword_ShouldReturnEncodedString_WhenPasswordIsValid()
    {
        // Arrange
        var user = CreateDummyUser();
        var rawPassword = "StrongSecretPassword123!";

        // Act
        var hash = _hasher.HashPassword(user, rawPassword);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Contain("$argon2id$");
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_WhenPasswordMatches()
    {
        // Arrange
        var user = CreateDummyUser();
        var rawPassword = "StrongSecretPassword123!";
        var hash = _hasher.HashPassword(user, rawPassword);

        // Act
        var isValid = _hasher.VerifyPassword(user, hash, rawPassword);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordIsIncorrect()
    {
        // Arrange
        var user = CreateDummyUser();
        var rawPassword = "StrongSecretPassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = _hasher.HashPassword(user, rawPassword);

        // Act
        var isValid = _hasher.VerifyPassword(user, hash, wrongPassword);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenHashIsInvalid()
    {
        // Arrange
        var user = CreateDummyUser();

        // Act
        var isValid = _hasher.VerifyPassword(user, "invalid_hash_format", "any_password");

        // Assert
        isValid.Should().BeFalse();
    }
}
