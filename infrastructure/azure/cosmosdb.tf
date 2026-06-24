resource "azurerm_cosmosdb_account" "this" {
  name                = local.cosmosdb_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  free_tier_enabled                = false
  local_authentication_disabled    = false
  public_network_access_enabled    = true
  automatic_failover_enabled       = false
  multiple_write_locations_enabled = false

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = azurerm_resource_group.this.location
    failover_priority = 0
    zone_redundant    = false
  }
}

# Application databases and containers
resource "azurerm_cosmosdb_sql_database" "app" {
  name                = "whiskey-and-smokes"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
}

resource "azurerm_cosmosdb_sql_container" "items" {
  name                = "items"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/userId"]
}

resource "azurerm_cosmosdb_sql_container" "captures" {
  name                = "captures"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/userId"]
}

resource "azurerm_cosmosdb_sql_container" "users" {
  name                = "users"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/id"]
}

resource "azurerm_cosmosdb_sql_container" "prompts" {
  name                = "prompts"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/partitionKey"]
}

resource "azurerm_cosmosdb_sql_container" "oidc_providers" {
  name                = "oidc-providers"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/partitionKey"]
}

resource "azurerm_cosmosdb_sql_container" "oidc_auth_states" {
  name                = "oidc-auth-states"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/partitionKey"]
}

resource "azurerm_cosmosdb_sql_container" "external_identities" {
  name                = "external-identities"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/partitionKey"]
}

resource "azurerm_cosmosdb_sql_container" "settings" {
  name                = "settings"
  resource_group_name = azurerm_cosmosdb_account.this.resource_group_name
  account_name        = azurerm_cosmosdb_account.this.name
  database_name       = azurerm_cosmosdb_sql_database.app.name
  partition_key_paths = ["/partitionKey"]
}
