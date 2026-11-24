using System.Text.Json;
using PhotoMind.Core.DTOs;

namespace PhotoMind.Tests.DTOs;

public class DeepSeekContractsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Request_SerializesWithSnakeCaseKeys()
    {
        var request = new DeepSeekRequest
        {
            Model = "deepseek-flash",
            Messages =
            {
                new DeepSeekMessage { Role = "system", Content = "hello" },
            },
        };

        var json = JsonSerializer.Serialize(request, Web);

        Assert.Contains("\"model\":\"deepseek-flash\"", json);
        Assert.Contains("\"role\":\"system\"", json);
        Assert.Contains("\"content\":\"hello\"", json);
    }

    [Fact]
    public void VisionContentPart_SerializesImageUrlBlock()
    {
        var message = new DeepSeekMessage
        {
            Role = "user",
            Content = new List<DeepSeekContentPart>
            {
                new()
                {
                    Type = "image_url",
                    ImageUrl = new DeepSeekImageUrl { Url = "data:image/jpeg;base64,AAAA", Detail = "low" },
                },
            },
        };

        var json = JsonSerializer.Serialize(message, Web);

        Assert.Contains("\"type\":\"image_url\"", json);
        Assert.Contains("\"image_url\":", json);
        Assert.Contains("\"url\":\"data:image/jpeg;base64,AAAA\"", json);
        Assert.Contains("\"detail\":\"low\"", json);
    }

    [Fact]
    public void Response_DeserializesChoicesAndUsage()
    {
        const string json = """
        {
            "choices": [{"message": {"role": "assistant", "content": "{\"category\":\"自然\"}"}, "finish_reason": "stop"}],
            "usage": {"prompt_tokens": 435, "completion_tokens": 443, "total_tokens": 878}
        }
        """;

        var response = JsonSerializer.Deserialize<DeepSeekResponse>(json, Web);

        Assert.NotNull(response);
        var choice = Assert.Single(response.Choices);
        Assert.Equal("assistant", choice.Message.Role);
        Assert.Contains("自然", choice.Message.Content);
        Assert.Equal(435, response.Usage!.PromptTokens);
        Assert.Equal(443, response.Usage.CompletionTokens);
        Assert.Equal(878, response.Usage.TotalTokens);
    }

    [Fact]
    public void Response_DeserializesErrorBody()
    {
        const string json = """{"error": {"message": "Insufficient Balance", "type": "invalid_request_error", "code": "402"}}""";

        var response = JsonSerializer.Deserialize<DeepSeekResponse>(json, Web);

        Assert.NotNull(response?.Error);
        Assert.Contains("Balance", response.Error.Message);
        Assert.Equal("402", response.Error.Code);
    }
}
