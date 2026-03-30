resource "random_uuid" "guid" {}

locals {
  project_rg_name      = "${var.foundry_project.name}_rg"
}
