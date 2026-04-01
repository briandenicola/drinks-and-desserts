locals {
  # Derived from infrastructure naming convention
  core_rg_name      = "${var.app_name}-core_rg"
  apps_rg_name      = "${var.app_name}-app_rg"

  acr_name          = "${replace(var.app_name, "-", "")}acr"
  cae_name          = "${var.app_name}-env"

  appinsights_name     = "${var.app_name}-appinsights"
  loganalytics_name    = "${var.app_name}-logs"
  app_insights_name    = "${var.app_name}-appinsights"
  cosmosdb_name        = "${var.app_name}-db"
  aca_name             = "${var.app_name}-env"

  ai_services_name  = "${var.app_name}-foundry"
  project_name      = "${var.app_name}-project"

  identity_name     = "${var.app_name}-app-identity"
  api_app_name      = "${var.app_name}-api"
  web_app_name      = "${var.app_name}-web"

  # Container images
  acr_login_server  = "${local.acr_name}.azurecr.io"
  api_image         = "${local.acr_login_server}/whiskey-and-smokes-api:latest" #${var.commit_version}"
  web_image         = "${local.acr_login_server}/whiskey-and-smokes-web:latest" #${var.commit_version}"

  # Foundry endpoint
  foundry_project_endpoint = "https://${local.ai_services_name}.services.ai.azure.com/api/projects/${local.project_name}"
}
