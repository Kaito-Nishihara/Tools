
```
dotnet build DbDocs
pwsh .\DbDocs\bin\Debug\net10.0\playwright.ps1 install chromium


dotnet run --project DbDocs -- --connection "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" --out ".\db-docs" --title "NK Admin DB 定義書" --pdf
```


# Migration Command

```
dotnet ef migrations add Init_UserSchema --project NK.Entities --startup-project NK.Admin --context AppDbContext

# DB Update Command
```
```
dotnet ef database update --project NK.Entities --startup-project NK.Admin --context AppDbContext
```

```
pwsh .\scripts\generate-db-docs.ps1 `
  -ConnectionString "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" `
  -OutDir ".\db-docs\md"
```