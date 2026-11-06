```
dotnet add package Microsoft.EntityFrameworkCore.Design --version "8.*"
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version "8.*"
dotnet add package Microsoft.Extensions.Configuration --version "8.*"
dotnet add package Microsoft.Extensions.Configuration.Json --version "8.*"
```

## Kết nối csdl sử dụng EntityFrameworkCore

```
  Microsoft.EntityFrameworkCore.SQLServer
  Microsoft.EntityFrameworkCore.Design
```

List toan bo tool cua dotnet
`dotnet tool list --global`
Cài đặt
`dotnet tool install --global dotnet-ef --version 8.0.6`
Gen Model bằng EF

```
dotnet ef dbcontext scaffold "Server=(local);uid=sa;password=123;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SQLServer --output-dir Models
```

## Tạo `appsetting.json` để kết nối đến CSDL

Cài json (nếu không có)

```
Microsoft.Extensions.Configuration
Microsoft.Extensions.Configuration.Json
```

Tạo `appsettings.json`, Set property `Copy to output directory` = `Always`
Nội dung `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=(local);uid=sa;password=123;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

Thay đổi `OnConfiguring` trong `DBName_Context.cs`

```C#
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    var builder = new ConfigurationBuilder();
    builder.SetBasePath(Directory.GetCurrentDirectory());
    builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    var configuration = builder.Build();
    optionsBuilder.UseSqlServer(configuration.GetConnectionString("Default"));
}
```
