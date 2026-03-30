data "azurerm_client_config" "current" {}

# Look up existing infrastructure resources by convention
data "azurerm_container_registry" "this" {
  name                = local.acr_name
  resource_group_name = local.core_rg_name
}

data "azurerm_container_app_environment" "this" {
  name                = local.cae_name
  resource_group_name = local.core_rg_name
}

data "azurerm_cognitive_account" "ai_services" {
  name                = local.ai_services_name
  resource_group_name = local.ai_rg_name
}

data "azurerm_application_insights" "workflow" {
  name                = "${local.project_workflow}-appinsights"
  resource_group_name = "${local.project_workflow}_rg"
}