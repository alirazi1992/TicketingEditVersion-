# JWT Secret Configuration (IIS / Production)

The app requires a JWT secret in production. The following configuration keys are accepted (checked in priority order).

## Accepted configuration keys (priority order)

| Source | Key / Env var | Notes |
|--------|----------------|--------|
| Environment | `JWT_SECRET` | Plain env var |
| Config / env | `Jwt:Secret` or env `Jwt__Secret` | **Canonical** (double underscore maps to `:`) |
| Config / env | `JWT:Secret` or env `JWT__Secret` | Common when setting `JWT__Secret` in web.config |
| Config / env | `JwtSettings:Secret` or env `JwtSettings__Secret` | |
| Config / env | `Auth:Jwt:Secret` or env `Auth__Jwt__Secret` | |

Config values come from `appsettings.json`, `appsettings.Production.json`, or environment variables (with `__` → `:` mapping).

## Sample web.config (canonical env var)

Use the **canonical** key so the startup log shows a clear source:

```xml
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\Ticketing.Backend.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="Jwt__Secret" value="YOUR-PRODUCTION-SECRET-MIN-32-CHARS" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

Replace `YOUR-PRODUCTION-SECRET-MIN-32-CHARS` with a strong secret (do not commit real secrets). At startup the app logs which key was used (e.g. `[STARTUP] JWT secret source: Jwt:Secret (config / env Jwt__Secret)`) but never logs the secret value.
