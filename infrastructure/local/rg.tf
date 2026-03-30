resource "azurerm_resource_group" "this" {
  name     = "${local.resource_name}-app_rg"
  location = var.region
  tags = {
    Application = var.tags
    DeployedOn  = timestamp()
    AppName     = local.resource_name
    Tier        = "Azure OpenAI; Local Development Instance"
  }
}