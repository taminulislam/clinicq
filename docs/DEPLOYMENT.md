# ClinicQ deployment

Target platform is Azure App Service (Windows) with Azure SQL, Blob Storage, Key Vault and
Application Insights. Everything in `infra/main.bicep` is parameterised by name prefix,
environment, location and SKU, so the same template builds Dev, Test and Prod.

## Azure resources

| Resource | Name pattern | Purpose |
| --- | --- | --- |
| App Service plan | `clinicq-<env>-plan` | Hosts the web app. `S1` or higher for Prod (slots and Always On). |
| Web app | `clinicq-<env>-web` | The ClinicQ site. System-assigned managed identity, HTTPS only, health check on `/health`. |
| Deployment slot | `clinicq-<env>-web/staging` | Warm target for releases; swapped into production. Has its own managed identity. |
| Azure SQL server | `clinicq-<env>-sql` | Logical server, TLS 1.2 minimum. |
| Azure SQL database | `clinicq-<env>-db` | Application database (`S0` by default). |
| Storage account | `clinicq<env><hash>st` | Blob container `lab-reports` for uploaded results. |
| Key Vault | `clinicq<env><hash>kv` | Connection strings, JWT signing key, SendGrid key. Soft delete on; purge protection in Prod. |
| Application Insights | `clinicq-<env>-ai` | Telemetry, workspace-based. |
| Log Analytics workspace | `clinicq-<env>-logs` | Backing workspace for App Insights. |

Deploy the infrastructure:

```bash
az group create --name clinicq-dev-rg --location centralus

az deployment group create \
  --resource-group clinicq-dev-rg \
  --template-file infra/main.bicep \
  --parameters namePrefix=clinicq environmentName=dev appServicePlanSku=S1 \
               sqlAdministratorLogin=clinicqadmin \
               sqlAdministratorPassword='<from-your-secret-store>' \
               administratorObjectId=$(az ad signed-in-user show --query id -o tsv)
```

`sqlAdministratorPassword` is a `@secure()` parameter: pass it from a pipeline secret variable or
a Key Vault reference, never from a file in the repository.

## Identity and secrets

The web app and its staging slot each get a **system-assigned managed identity**. The template
grants both `get`/`list` on Key Vault secrets, and `Storage Blob Data Contributor` on the storage
account for the production identity.

Application settings are Key Vault references, resolved at startup by the identity:

```
ConnectionStrings__Default        -> @Microsoft.KeyVault(VaultName=<kv>;SecretName=ConnectionStrings--Default)
Jwt__SigningKey                   -> @Microsoft.KeyVault(VaultName=<kv>;SecretName=Jwt--SigningKey)
SendGrid__ApiKey                  -> @Microsoft.KeyVault(VaultName=<kv>;SecretName=SendGrid--ApiKey)
Storage__AzureBlobConnectionString-> @Microsoft.KeyVault(VaultName=<kv>;SecretName=Storage--AzureBlobConnectionString)
```

Notes:

- The double underscore is the .NET configuration separator for nested keys; the colon form does
  not survive as an environment variable on Linux.
- The seeded SQL connection string uses `Authentication=Active Directory Default`, so the app
  authenticates to Azure SQL with its managed identity and no password is stored. Create the
  contained user once per database:

  ```sql
  CREATE USER [clinicq-prod-web] FROM EXTERNAL PROVIDER;
  ALTER ROLE db_datareader ADD MEMBER [clinicq-prod-web];
  ALTER ROLE db_datawriter ADD MEMBER [clinicq-prod-web];
  GRANT EXECUTE ON SCHEMA::dbo TO [clinicq-prod-web];   -- stored procedures
  ```

- Rotate the JWT signing key by adding a new secret version; App Service picks it up on restart.
  Existing tokens stay valid until they expire (60 minutes by default).
- Nothing in `appsettings.json` contains a secret. Locally, use `dotnet user-secrets` or leave the
  values empty and run against SQLite.

## Database deployment

The scripts in `db/` are idempotent and run in order:

1. `001_schema.sql` - tables, constraints and indexes (`IF OBJECT_ID(...) IS NULL` guards).
2. `002_seed.sql` - branches, slot rules, doctors, fee schedule, users and sample patients; every
   insert is guarded by an emptiness check, so re-running never duplicates rows.
3. `003_procs.sql` - `usp_GetAvailableSlots`, `usp_ReconcileBilling`, `usp_GetBranchRevenue`, all
   `CREATE OR ALTER`.

The pipeline applies them to the Dev database after deployment using an access token from the
service connection. For Prod, run them as a gated step during the release window.

Schema changes are forward-only: add a new numbered script rather than editing an applied one.

## Pipeline

`azure-pipelines.yml` defines four stages:

1. **Build** - restore, build, publish the web app as a zip, stage `db/`, `infra/` and the Postman
   collection, publish the `drop` artifact.
2. **Test** - `dotnet test` with Cobertura coverage and TRX results, then a Newman job that starts
   the app on the SQLite fallback and runs `tests/postman/ClinicQ.postman_collection.json`.
3. **DeployDev** - `AzureWebApp@1` to `clinicq-dev-web`, then applies the database scripts.
   Runs only on `main`, against the `dev` environment.
4. **DeployProd** - deploys to the `staging` slot, smoke tests `/health` on the slot, then
   `AzureAppServiceManage@0` swaps staging into production. Gated by the `prod` environment, which
   is where approvals and business-hours checks are configured.

Slot swap is the rollback mechanism: swapping back restores the previous build in seconds.

## Custom DNS and TLS

1. Add the hostname in the web app's **Custom domains** blade and note the verification ID.
2. Create the DNS records at the registrar (or in an Azure DNS zone):

   ```
   CNAME  clinic   clinicq-prod-web.azurewebsites.net
   TXT    asuid.clinic  <verification id>
   ```

3. Validate and add the binding, then create an **App Service Managed Certificate** and set the
   binding to SNI SSL. The app already sets `httpsOnly`, so HTTP is redirected.
4. Add the hostname to `AllowedHosts` in configuration.

## Scaling and operations

- **Scale out** before scaling up; the app is stateless apart from Hangfire's in-memory storage.
  Move Hangfire to `Hangfire.SqlServer` before running more than one instance, otherwise every
  instance runs the recurring jobs independently.
- **Always On** must be enabled (it is, for `S1`+) so the Hangfire server is not unloaded.
- **Health check path** `/health` is configured on the site; App Service removes unhealthy
  instances from rotation.
- **Monitoring** - Serilog writes to the console (captured by App Service logs). To enable the
  Application Insights sink, add `Serilog.Sinks.ApplicationInsights` and the `WriteTo` line
  documented in `Program.cs`; the connection string is already an app setting.
- **Backups** - Azure SQL point-in-time restore covers the database. Blob storage should have soft
  delete and versioning enabled for lab reports.

## Environments

| Environment | Slot | Database | Seeding |
| --- | --- | --- | --- |
| Local | - | SQLite `app.db` | Full demo data on first run |
| Dev | production slot | `clinicq-dev-db` | Reference data from `002_seed.sql` |
| Prod | `staging` -> production | `clinicq-prod-db` | Reference data only; set `Seed:DemoActivity=false` |

Set `Seed:Enabled=false` in Prod once real data exists.
