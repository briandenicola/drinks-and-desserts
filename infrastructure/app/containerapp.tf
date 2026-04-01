# ── API Container App ──────────────────────────────────────
resource "azurerm_container_app" "api" {
  name                         = local.api_app_name
  container_app_environment_id = data.azurerm_container_app_environment.this.id
  resource_group_name          = local.apps_rg_name
  revision_mode                = "Single"

  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.app.id
    ]
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 8080
    transport                  = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  registry {
    server   = local.acr_login_server
    identity = azurerm_user_assigned_identity.app.id
  }

  template {
    container {
      name   = "api"
      image  = local.api_image
      cpu    = 1
      memory = "2Gi"

      env {
        name  = "AiFoundry__ProjectEndpoint"
        value = local.foundry_project_endpoint
      }

      env {
        name  = "AiFoundry__Models__Vision"
        value = "gpt-4o"
      }

      env {
        name  = "AiFoundry__Models__Reasoning"
        value = "gpt-5-mini"
      }

      env {
        name  = "CosmosDb__Endpoint"
        value = var.cosmosdb_endpoint
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "whiskey-and-smokes"
      }

      env {
        name  = "BlobStorage__Endpoint"
        value = var.storage_blob_endpoint
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = data.azurerm_application_insights.this.connection_string
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app.client_id
      }
    }

    max_replicas = 3
    min_replicas = 1
  }
}

# ── Web Container App ──────────────────────────────────────
resource "azurerm_container_app" "web" {
  name                         = local.web_app_name
  container_app_environment_id = data.azurerm_container_app_environment.this.id
  resource_group_name          = local.apps_rg_name
  revision_mode                = "Single"

  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.app.id
    ]
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 80
    transport                  = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  registry {
    server   = local.acr_login_server
    identity = azurerm_user_assigned_identity.app.id
  }

  template {
    container {
      name   = "web"
      image  = local.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "API_HOST"
        value = "${local.api_app_name}.internal.${data.azurerm_container_app_environment.this.default_domain}"
      }

      env {
        name  = "API_PORT"
        value = "443"
      }
    }

    max_replicas = 3
    min_replicas = 1
  }
}
