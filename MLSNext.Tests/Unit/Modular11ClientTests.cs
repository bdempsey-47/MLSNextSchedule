using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;
using MLSNext.Ingestion.Services;
using Microsoft.Extensions.Logging;
using static Moq.It;

namespace MLSNext.Tests.Unit;

public class Modular11ClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<Modular11Client>> _loggerMock;
    private readonly Modular11Settings _settings;

    public Modular11ClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<Modular11Client>>();
        
        _settings = new Modular11Settings
        {
            TournamentId = "35",
            Gender = "1",
            Status = "scheduled",
            MatchType = "2",
            AgeGroups = new List<string> { "13", "15", "17" },
            StartDate = "",
            EndDate = ""
        };
    }

    [Fact]
    public async Task FetchPageAsync_WithValidPage_ReturnsHtmlContent()
    {
        // Arrange
        var client = new Modular11Client(_httpClient, _loggerMock.Object, _settings);
        var expectedContent = "<html><body>Match data</body></html>";
        
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(expectedContent)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                IsAny<HttpRequestMessage>(),
                IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await client.FetchPageAsync(1);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task FetchPageAsync_WithHttpFailure_ThrowsException()
    {
        // Arrange
        var client = new Modular11Client(_httpClient, _loggerMock.Object, _settings);
        
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                IsAny<HttpRequestMessage>(),
                IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchPageAsync(1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public async Task FetchPageAsync_IncludesPageNumberInQuery(int pageNumber)
    {
        // Arrange
        var client = new Modular11Client(_httpClient, _loggerMock.Object, _settings);
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                Is<HttpRequestMessage>(req => 
                    req.RequestUri!.Query.Contains($"open_page={pageNumber}")),
                IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        await client.FetchPageAsync(pageNumber);

        // Assert - Verify the mock was called with the correct page number
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            Is<HttpRequestMessage>(req => 
                req.RequestUri!.Query.Contains($"open_page={pageNumber}")),
            IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchPageAsync_IncludesRequiredQueryParameters()
    {
        // Arrange
        var client = new Modular11Client(_httpClient, _loggerMock.Object, _settings);
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                IsAny<HttpRequestMessage>(),
                IsAny<CancellationToken>())
            .ReturnsAsync(response)
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                var query = req.RequestUri!.Query;
                query.Should().Contain("tournament=35");
                query.Should().Contain("gender=1");
                query.Should().Contain("status=scheduled");
                query.Should().Contain("match_type=2");
            });

        // Act
        await client.FetchPageAsync(1);

        // Assert
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            IsAny<HttpRequestMessage>(),
            IsAny<CancellationToken>());
    }
}
