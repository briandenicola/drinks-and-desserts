resource "azurerm_role_assignment" "local_user_openai_user" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "ai_foundry_owner" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Azure AI Developer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "ai_foundry_owner_project_manager" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Azure AI Project Manager"
  principal_id         = data.azurerm_client_config.current.object_id
}