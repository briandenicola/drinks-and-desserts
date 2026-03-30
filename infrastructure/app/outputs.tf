output "CLASSIC_URL" {
  value = "https://${azurerm_container_app.classic.ingress[0].fqdn}"
}

output "WORKFLOW_URL" {
  value = "https://${azurerm_container_app.workflow.ingress[0].fqdn}"
}

output "MANAGED_IDENTITY_CLIENT_ID" {
  value = azurerm_user_assigned_identity.app.client_id
}
