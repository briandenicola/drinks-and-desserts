resource "azurerm_resource_group" "apps" {
  name     = local.apps_rg_name
  location = var.region
  tags = {
    Application = var.tags
    Components  = "Container Apps, Managed Identity"
    DeployedOn  = timestamp()
  }
}