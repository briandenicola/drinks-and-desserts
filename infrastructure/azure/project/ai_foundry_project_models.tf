
resource "azapi_resource" "model_deployment_gpt51" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2025-06-01"
  name      = var.foundry_project.models[0].name
  parent_id = var.foundry_project.ai_foundry.id

  body = {
    properties = {
      model = {
        format  = var.foundry_project.models[0].format
        name    = var.foundry_project.models[0].name
        version = var.foundry_project.models[0].version
      }
    }
    sku = {
      name     = "GlobalStandard"
      capacity = 100
    }
  }
  depends_on = [azapi_resource.ai_foundry_project]
}

resource "azapi_resource" "model_deployment_gpt54-mini" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2025-06-01"
  name      = var.foundry_project.models[1].name
  parent_id = var.foundry_project.ai_foundry.id

  body = {
    properties = {
      model = {
        format  = var.foundry_project.models[1].format
        name    = var.foundry_project.models[1].name
        version = var.foundry_project.models[1].version
      }
    }
    sku = {
      name     = "GlobalStandard"
      capacity = 100
    }
  }

  depends_on = [azapi_resource.model_deployment_gpt51, azapi_resource.ai_foundry_project]
}
