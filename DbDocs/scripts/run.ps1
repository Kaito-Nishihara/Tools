dotnet run -- --connection "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" --out ".\db-docs" --title "NK Admin DB 定義書" --pdf
& pwsh .\scripts\generate-db-docs.ps1 `
    -ConnectionString "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" `
    -OutDir ".\db-docs\md"