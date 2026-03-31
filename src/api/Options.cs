namespace WhiskeyAndSmokes.Api;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "whiskey-and-smokes";
    public string Audience { get; set; } = "whiskey-and-smokes";
    public int ExpirationDays { get; set; } = 7;
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
    public string Vision { get; set; } = "gpt-4o";
    public string Reasoning { get; set; } = "gpt-5-mini";
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
