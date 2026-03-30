
module "project_1" {
  depends_on = [
    azapi_resource.ai_foundry,
  ]
  source   = "./project"

  foundry_project = {
    name          = local.project_name
    location      = local.location
    resource_name = local.resource_name
    tag           = var.tags

    ai_foundry    = {
      id = azapi_resource.ai_foundry.id
      name = azapi_resource.ai_foundry.name
    }

    logs          = {
      workspace_id = azurerm_log_analytics_workspace.this.id
    }

    models = [
      {
        name    = "gpt-4o"
        version = "2024-11-20"
        format  = "OpenAI"
      },
      {
        name    = "gpt-5-mini"
        version = "2025-08-07"
        format  = "OpenAI"
      }
    ]
  }
}
