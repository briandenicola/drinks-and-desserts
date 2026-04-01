variable "app_name" {
  description = "Base resource name from infrastructure (terraform output APP_NAME)"
  type        = string
}

variable "region" {
  description = "Region to deploy resources to (must match infrastructure)"
}

variable "tags" {
  description = "Application tag for resource groups"
  type        = string
}

variable "commit_version" {
  description = "Container image tag (short git SHA)"
  type        = string
}

variable "cosmosdb_endpoint" {
  description = "Cosmos DB account endpoint (from azure stack output)"
  type        = string
}

variable "storage_blob_endpoint" {
  description = "Storage account blob endpoint (from azure stack output)"
  type        = string
}