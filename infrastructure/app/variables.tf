variable "app_name" {
  description = "Base resource name from infrastructure (terraform output APP_NAME)"
  type        = string
}

variable "region" {
  description = "Region to deploy resources to (must match infrastructure)"
  default     = "eastus2"
}

variable "tags" {
  description = "Application tag for resource groups"
  type        = string
}

variable "commit_version" {
  description = "Container image tag (short git SHA)"
  type        = string
}