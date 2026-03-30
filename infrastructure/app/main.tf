locals {
  # Derived from infrastructure naming convention
  core_rg_name      = "${var.app_name}-core_rg"
  ai_rg_name        = "${var.app_name}-ai_rg"
  acr_name          = "${replace(var.app_name, "-", "")}acr"
  cae_name          = "${var.app_name}-env"
  ai_services_name  = "${var.app_name}-foundry"

  # Project names follow infrastructure convention
  project_workflow  = "${var.app_name}-project-workflow"

  # App resource names
  apps_rg_name      = "${var.app_name}_apps_rg"
  identity_name     = "${var.app_name}-app-identity"
  workflow_app_name = "${var.app_name}-workflow"

  # Container images
  acr_login_server  = "${local.acr_name}.azurecr.io"  
  workflow_image    = "${local.acr_login_server}/loan-origination-workflow:${var.commit_version}"

  # Foundry endpoints
  foundry_endpoint_workflow = "https://${local.ai_services_name}.services.ai.azure.com/api/projects/${local.project_workflow}"
}
