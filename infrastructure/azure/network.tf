resource "azurerm_virtual_network" "this" {
  name                = local.vnet_name
  address_space       = [local.vnet_cidr]
  location            = azurerm_resource_group.core.location
  resource_group_name = azurerm_resource_group.core.name
}

resource "azurerm_subnet" "private-endpoints" {
  name                 = "private-endpoints"
  resource_group_name  = azurerm_resource_group.core.name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.pe_subnet_cidr]
}

resource "azurerm_subnet" "nodes" {
  lifecycle {
    ignore_changes = [
      delegation
    ]
  }

  name                 = "nodes"
  resource_group_name  = azurerm_resource_group.core.name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.nodes_subnet_cidir]

  delegation {
    name = "container-apps"

    service_delegation {
      name = "Microsoft.App/environments"
    }
  }

}

resource "azurerm_network_security_group" "this" {
  name                = local.nsg_name
  location            = azurerm_resource_group.core.location
  resource_group_name = azurerm_resource_group.core.name

  security_rule {
    name                   = "HTTPS"
    priority               = 100
    direction              = "Inbound"
    access                 = "Allow"
    protocol               = "Tcp"
    source_port_range      = "*"
    destination_port_range = "443"
    #trivy:ignore:AZU-0047 Public-facing HTTPS ingress from any source is required for Azure Container Apps load balancer
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

resource "azurerm_subnet_network_security_group_association" "pe" {
  subnet_id                 = azurerm_subnet.private-endpoints.id
  network_security_group_id = azurerm_network_security_group.this.id
}

resource "azurerm_subnet_network_security_group_association" "nodes" {
  subnet_id                 = azurerm_subnet.nodes.id
  network_security_group_id = azurerm_network_security_group.this.id
}
