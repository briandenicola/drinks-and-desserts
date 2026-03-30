resource "azurerm_user_assigned_identity" "app" {
  name                = local.identity_name
  resource_group_name = azurerm_resource_group.apps.name
  location            = azurerm_resource_group.apps.location
}
