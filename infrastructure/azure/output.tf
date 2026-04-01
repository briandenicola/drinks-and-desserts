output "APP_NAME" {
  value     = local.resource_name
  sensitive = false
}

output "APP_LOCATION" {
  value     = var.region
  sensitive = false
}
output "APP_RESOURCE_GROUP" {
  value     = azurerm_resource_group.this.name
  sensitive = false
}

output "OPENAI_ENDPOINT" {
  value     = data.azurerm_cognitive_account.ai_foundry.endpoint
  sensitive = false
}

output "FOUNDRY_PROJECT_ENDPOINT" {
  value     = module.project_1.PROJECT_ENDPOINT
  sensitive = false
}

output "ACR_NAME" {
  value     = azurerm_container_registry.this.name
  sensitive = false
}

output "APPINSIGHTS_CONNECTION_STRING" {
  value     = azurerm_application_insights.this.connection_string
  sensitive = true
}

output "COSMOSDB_ENDPOINT" {
  value     = azurerm_cosmosdb_account.this.endpoint
  sensitive = false
}

output "COSMOSDB_ACCOUNT_NAME" {
  value     = azurerm_cosmosdb_account.this.name
  sensitive = false
}

output "STORAGE_ACCOUNT_NAME" {
  value     = azurerm_storage_account.this.name
  sensitive = false
}

output "STORAGE_BLOB_ENDPOINT" {
  value     = azurerm_storage_account.this.primary_blob_endpoint
  sensitive = false
}