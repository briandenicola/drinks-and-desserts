namespace WhiskeyAndSmokes.Api.Configuration;

public class CosmosDbConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "whiskey-and-smokes";
    public string[] Containers { get; set; } = ["users", "captures", "items", "venues"];
}
