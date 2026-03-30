output "APP_NAME" {
  value     = local.resource_name
  sensitive = false
}

output "APP_RESOURCE_GROUP" {
  value     = azurerm_resource_group.this.name
  sensitive = false
}

output "OPENAI_ENDPOINT" {
  value     = azapi_resource.ai_foundry.output.properties.endpoint
  sensitive = false
}

output "APPLICATION_INSIGHTS_CONNECTION_STRING" {
  value     = azure
  sensitive = false
}
