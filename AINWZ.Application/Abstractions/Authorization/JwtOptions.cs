namespace SpeakEase.Write.Application.Abstractions.Authorization;

public sealed class JwtOptions
{
    public string SecretKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int ExpMinutes { get; set; } = 30;
    public int RefreshExpire { get; set; } = 60;
}
