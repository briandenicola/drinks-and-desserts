resource "azurerm_storage_account" "this" {
  name                = local.storage_account_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  account_kind              = "StorageV2"
  account_tier              = "Standard"
  account_replication_type  = "LRS"
  shared_access_key_enabled = true

  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false

  network_rules {
    default_action             = "Deny"
    ip_rules                   = []
    virtual_network_subnet_ids = [azurerm_subnet.nodes.id]
    bypass                     = ["AzureServices"]
  }
}

resource "azurerm_storage_container" "photos" {
  name                  = "photos"
  storage_account_id    = azurerm_storage_account.this.id
  container_access_type = "private"
}
