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

variable "jwt_secret" {
  description = "JWT signing secret for local application tokens. Must be at least 32 characters."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "jwt_secret must be at least 32 characters."
  }
}

variable "oidc_public_origin" {
  description = "Canonical public web origin used to build OIDC redirect URIs, for example https://app.example.com."
  type        = string
  default     = ""
}

variable "entra_tenant_id" {
  description = "Optional Microsoft Entra tenant ID for sign-in."
  type        = string
  default     = ""
}

variable "entra_client_id" {
  description = "Optional Microsoft Entra app registration client ID for sign-in."
  type        = string
  default     = ""
}

variable "entra_audience" {
  description = "Optional accepted Entra token audience. Defaults to entra_client_id when empty."
  type        = string
  default     = ""
}
