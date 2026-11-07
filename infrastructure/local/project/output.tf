output "PROJECT_NAME" {
  value     = var.foundry_project.name
  sensitive = false
}

output "PROJECT_ENDPOINT" {
  value     = "https://${var.foundry_project.ai_foundry.name}.services.ai.azure.com/api/projects/${var.foundry_project.name}"
  sensitive = false
}
output "MODEL_DEPLOYMENT_NAMES" {
  description = "Names of the model deployments created in this project, in the order declared."
  value       = [for m in var.foundry_project.models : m.name]
}
