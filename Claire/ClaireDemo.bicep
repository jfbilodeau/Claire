resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'democlaire123'
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Cool'
  }
}