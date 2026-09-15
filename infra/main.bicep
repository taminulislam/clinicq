@description('Prefix for every resource name, e.g. clinicq.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'clinicq'

@description('Deployment environment discriminator.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service plan SKU.')
@allowed([
  'B1'
  'S1'
  'P1v3'
])
param appServicePlanSku string = 'S1'

@description('Azure SQL database SKU.')
@allowed([
  'Basic'
  'S0'
  'S1'
])
param sqlDatabaseSku string = 'S0'

@description('Azure SQL administrator login.')
param sqlAdministratorLogin string

@description('Azure SQL administrator password. Pass from a secure pipeline variable or Key Vault.')
@secure()
param sqlAdministratorPassword string

@description('Entra ID object id granted Key Vault secret access alongside the web app identity.')
param administratorObjectId string = ''

var resourceToken = '${namePrefix}-${environmentName}'
var uniqueToken = toLower('${namePrefix}${environmentName}${uniqueString(resourceGroup().id)}')
var webAppName = '${resourceToken}-web'
var planName = '${resourceToken}-plan'
var sqlServerName = '${resourceToken}-sql'
var sqlDatabaseName = '${resourceToken}-db'
var keyVaultName = take('${uniqueToken}kv', 24)
var storageAccountName = take('${uniqueToken}st', 24)
var appInsightsName = '${resourceToken}-ai'
var logAnalyticsName = '${resourceToken}-logs'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: false
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource labReportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'lab-reports'
  properties: {
    publicAccess: 'None'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: sqlDatabaseSku
    tier: sqlDatabaseSku == 'Basic' ? 'Basic' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

// Allows other Azure services (including App Service) to reach the logical server.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: appServicePlanSku != 'B1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Staging'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        // Secrets resolve through the system-assigned identity's Key Vault access.
        {
          name: 'ConnectionStrings__Default'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ConnectionStrings--Default)'
        }
        {
          name: 'Jwt__SigningKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Jwt--SigningKey)'
        }
        {
          name: 'SendGrid__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SendGrid--ApiKey)'
        }
        {
          name: 'Storage__AzureBlobConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Storage--AzureBlobConnectionString)'
        }
        {
          name: 'Storage__ContainerName'
          value: labReportsContainer.name
        }
      ]
    }
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: webApp
  name: 'staging'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: appServicePlanSku != 'B1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ConnectionStrings__Default'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ConnectionStrings--Default)'
        }
        {
          name: 'Jwt__SigningKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Jwt--SigningKey)'
        }
      ]
    }
  }
}

// Access policies cover both the production site and the staging slot identities.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: environmentName == 'prod' ? true : null
    enabledForTemplateDeployment: true
    accessPolicies: concat([
      {
        tenantId: subscription().tenantId
        objectId: webApp.identity.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
      {
        tenantId: subscription().tenantId
        objectId: stagingSlot.identity.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ], empty(administratorObjectId) ? [] : [
      {
        tenantId: subscription().tenantId
        objectId: administratorObjectId
        permissions: {
          secrets: [
            'get'
            'list'
            'set'
            'delete'
          ]
        }
      }
    ])
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ConnectionStrings--Default'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource storageConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Storage--AzureBlobConnectionString'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  }
}

@description('Blob Data Contributor for the web app identity, so lab report uploads work with managed identity.')
resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, webApp.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
output stagingSlotHostName string = stagingSlot.properties.defaultHostName
output keyVaultName string = keyVault.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output webAppPrincipalId string = webApp.identity.principalId
