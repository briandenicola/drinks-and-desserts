locals {
  location          = var.region
  resource_name     = "${random_pet.this.id}-${random_id.this.dec}"
  project_name      = "${local.resource_name}-project"
  ai_services_name  = "${local.resource_name}-foundry"
  loganalytics_name = "${local.resource_name}-logs"
}
