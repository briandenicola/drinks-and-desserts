# Pull container images from ACR
resource "azurerm_role_assignment" "acr_pull" {
  scope                            = data.azurerm_container_registry.this.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_user_assigned_identity.app.principal_id
  skip_service_principal_aad_check = true
}

# Allow the container apps to call Foundry agents via Entra ID
resource "azurerm_role_assignment" "cognitive_services_user" {
  scope                            = data.azurerm_cognitive_account.ai_services.id
  role_definition_name             = "Cognitive Services User"
  principal_id                     = azurerm_user_assigned_identity.app.principal_id
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "ai_developer" {
  scope                            = data.azurerm_cognitive_account.ai_services.id
  role_definition_name             = "Azure AI Developer"
  principal_id                     = azurerm_user_assigned_identity.app.principal_id
  skip_service_principal_aad_check = true
}

# Cosmos DB data plane access for application data
resource "azurerm_cosmosdb_sql_role_assignment" "cosmos_contributor" {
  resource_group_name = local.core_rg_name
  account_name        = local.cosmosdb_name
  scope               = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/${local.core_rg_name}/providers/Microsoft.DocumentDB/databaseAccounts/${local.cosmosdb_name}"
  role_definition_id  = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/${local.core_rg_name}/providers/Microsoft.DocumentDB/databaseAccounts/${local.cosmosdb_name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_user_assigned_identity.app.principal_id
}

# Blob storage access for photo uploads
resource "azurerm_role_assignment" "storage_blob_contributor" {
  scope                            = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/${local.apps_rg_name}"
  role_definition_name             = "Storage Blob Data Contributor"
  principal_id                     = azurerm_user_assigned_identity.app.principal_id
  skip_service_principal_aad_check = true
}
