namespace WhiskeyAndSmokes.Api;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "whiskey-and-smokes";
    public string Audience { get; set; } = "whiskey-and-smokes";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 90;
}

public class EntraIdOptions
{
    public const string Section = "EntraId";

    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(ClientId);
}

public class AiFoundryOptions
{
    public const string Section = "AiFoundry";

    public string Endpoint { get; set; } = string.Empty;
    public string ProjectEndpoint { get; set; } = string.Empty;
    public AiFoundryModelOptions Models { get; set; } = new();
}

public class AiFoundryModelOptions
{
    public string Vision { get; set; } = "gpt-5.1";
    public string Reasoning { get; set; } = "gpt-5.4-mini";
}

public class CosmosDbOptions
{
    public const string Section = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "whiskey-and-smokes";
}

public class BlobStorageOptions
{
    public const string Section = "BlobStorage";

    public string Endpoint { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "captures";
}
