using System.ComponentModel.DataAnnotations;

namespace Crawler.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required] public string Host { get; init; } = "localhost";
    [Required] public string VirtualHost { get; init; } = "/";
    [Required] public string Username { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}
