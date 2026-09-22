using System.Text;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Services;

public class TokenServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly TokenService _tokenService;
    private readonly ISession _session;

    public TokenServiceTests()
    {
        _session = new TestSession();
        var httpContext = new DefaultHttpContext { Session = _session };

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _tokenService = new TokenService(_httpContextAccessorMock.Object);
    }

    [Fact]
    public void SaveTokens_And_GetTokens_SavesAndRetrievesCorrectly()
    {
        // Arrange
        var accessToken = "test-access-token";
        var refreshToken = "test-refresh-token";
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        // Act
        _tokenService.SaveTokens(accessToken, refreshToken, expiresAt);

        // Assert
        _tokenService.GetAccessToken().Should().Be(accessToken);
        _tokenService.GetRefreshToken().Should().Be(refreshToken);
        _tokenService.GetTokenExpiry().Should().BeCloseTo(expiresAt, precision: TimeSpan.FromSeconds(1));
        _tokenService.HasValidAccessToken().Should().BeTrue();
    }

    [Fact]
    public void ClearTokens_RemovesAllStoredTokens()
    {
        // Arrange
        _tokenService.SaveTokens("token1", "refresh1", DateTime.UtcNow.AddHours(1));

        // Act
        _tokenService.ClearTokens();

        // Assert
        _tokenService.GetAccessToken().Should().BeNull();
        _tokenService.GetRefreshToken().Should().BeNull();
        _tokenService.GetTokenExpiry().Should().BeNull();
        _tokenService.HasValidAccessToken().Should().BeFalse();
    }

    [Fact]
    public void HasValidAccessToken_ReturnsFalse_WhenExpired()
    {
        // Arrange
        _tokenService.SaveTokens("expired-token", "refresh", DateTime.UtcNow.AddMinutes(-10));

        // Act
        var isValid = _tokenService.HasValidAccessToken();

        // Assert
        isValid.Should().BeFalse();
    }
}

// In-memory test session implementation
internal class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id => "test-session-id";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
