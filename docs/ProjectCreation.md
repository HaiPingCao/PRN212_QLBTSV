# Cách dự án (.Net solution) được tạo

## Khởi tạo dự án

```bash
# Tạo thư mục
mkdir QLBTSV_re
cd QLBTSV_re
# Tạo solution và các project con
dotnet new sln -n QLBTSV_re
dotnet new classlib -n QLBTSV_re.Shared
dotnet new wpf -n QLBTSV_re.Server
dotnet new wpf -n QLBTSV_re.Client
# Thêm project vào solution
dotnet sln add QLBTSV_re.Shared
dotnet sln add QLBTSV_re.Server
dotnet sln add QLBTSV_re.Client
# Liên kết vào thư viện chung
dotnet add QLBTSV_re.Server reference QLBTSV_re.Shared
dotnet add QLBTSV_re.Client reference QLBTSV_re.Shared
# Thêm các phụ thuộc (dependency)
dotnet add QLBTSV_re.Server package BCrypt.Net-Next --version 4.2.0
dotnet add QLBTSV_re.Server package Microsoft.Extensions.Configuration --version 8.0.0
dotnet add QLBTSV_re.Server package Microsoft.Extensions.Configuration.Json --version 8.0.1
dotnet add QLBTSV_re.Server package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.28
dotnet add QLBTSV_re.Server package Microsoft.EntityFrameworkCore.Design --version 8.0.28
```

## Tạo Models bằng EF

Sau đó tạo CSDL và Models bằng EF:

```bash
cd db
sqlcmd -S "(local)" -i QLBTSV_re.sql
```

```bash
dotnet ef dbcontext scaffold "Server=(local);uid=sa;password=<password>;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --force
```

Tạo `appsettings.json` (set `Copy to Output Directory = Always`):

```json
{
  "ConnectionStrings": {
    "Default": "Server=(local);uid=sa;password=<password>;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;"
  }
}
```
