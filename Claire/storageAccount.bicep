// Bicep template to create an Azure storage account named 'jfdemostor' in Canada East using LRS

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'jfdemostor'
  location: 'canadaeast'
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
}