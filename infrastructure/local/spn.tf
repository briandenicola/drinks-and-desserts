resource "azuread_application" "this" {
  display_name = "${local.resource_name}-whiskey-user"
}

resource "azuread_service_principal" "this" {
  client_id = azuread_application.this.client_id
}

resource "azuread_application_password" "this" {
  application_id = azuread_application.this.id
  display_name   = "${local.resource_name}-whiskey-user"
  end_date       = timeadd(timestamp(), "168h") # 1 year
  lifecycle {
    ignore_changes = [end_date]
  }
}


