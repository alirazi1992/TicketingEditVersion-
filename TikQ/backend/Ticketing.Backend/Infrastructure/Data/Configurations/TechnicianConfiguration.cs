{
"Database": {
"Provider": "SqlServer",
"AutoMigrateOnStartup": true
},
"SupervisorTechnicians": {
"Mode": "LinkedOnly"
},
"ConnectionStrings": {
"DefaultConnection": "Server=localhost;Database=TicketingDb;Trusted_Connection=True;TrustServerCertificate=True;"
},
"WindowsAuth": {
"Enabled": false,
"Mode": "Off"
},
"BootstrapAdmin": {
"Email": "admin@local",
"Password": "",
"FullName": "System Admin"
},
"CompanyDirectory": {
"Enabled": false,
"ConnectionString": "Server=.;Database=CompanyDb;User Id=...;Password=...;",
"Mode": "Enforce"
},
"EmergencyAdmin": {
"Enabled": false,
"Email": "",
"FullName": "Emergency Admin",
"Password": "",
"Key": ""
},
"Jwt": {
"Secret": "a9F#kL92!xPq7ZrT5vW8yB3@cD6EfG1hJkLmN0pQ",
"Issuer": "TicketingBackend",
"Audience": "TicketingFrontend",
"ExpirationMinutes": 240
},
"AuthCookies": {
"SameSite": "Lax",
"SecurePolicy": "SameAsRequest"
},
"Logging": {
"LogLevel": {
"Default": "Information",
"Microsoft.AspNetCore": "Warning",
"Microsoft.EntityFrameworkCore.Database.Command": "Warning"
}
},
"Cors": {
"AllowedOrigins": [
"http://localhost:3000",
"https://localhost:3000",
"http://localhost:3001",
"https://localhost:3001"
]
},
"AllowedHosts": "*"
}
