# Cách dự án (.Net solution) được tạo

## 1. Khởi tạo dự án

```bash
# Tạo thư mục
mkdir QLBTSV_re
cd QLBTSV_re
# Tạo solution và các project con
dotnet new sln -n QLBTSV_re
dotnet new classlib -n QLBT.Shared
dotnet new wpf -n QLBT.Server
dotnet new wpf -n QLBT.Client
# Thêm project vào solution
dotnet sln add QLBT.Shared
dotnet sln add QLBT.Server
dotnet sln add QLBT.Client
# Liên kết vào thư viện chung
dotnet add QLBT.Server reference QLBT.Shared
dotnet add QLBT.Client reference QLBT.Shared
# Thêm các phụ thuộc (dependency)
dotnet add QLBT.Server package BCrypt.Net-Next --version 4.2.0
dotnet add QLBT.Server package Microsoft.Extensions.Configuration --version 8.0.0
dotnet add QLBT.Server package Microsoft.Extensions.Configuration.Json --version 8.0.1
dotnet add QLBT.Server package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.28
dotnet add QLBT.Server package Microsoft.EntityFrameworkCore.Design --version 8.0.28
```

## 2. Thêm Asp.Net Framework vào server

Thêm khối code sau vào `QLBT.Server.csproj` để thêm ASP.NET vào WPF đã có sẵn

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

## 3. Tạo Models cho server bằng EF

Sau đó tạo CSDL (chạy từ thư mục gốc repo):

```bash
sqlcmd -S "(local)" -i db/QLBT.sql
```

### Lệnh scaffold

Chạy trong thư mục project Server (`QLBTSV_re/QLBT.Server`):

```bash
dotnet ef dbcontext scaffold "Server=(local);uid=sa;password=<password>;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --no-onconfiguring --force
```

- Cờ `--no-onconfiguring`: EF **không sinh** hàm `OnConfiguring()` trong `Prn212PQlbtContext`.

#### Cách hoạt động

`DbContext` cần biết dùng CSDL nào (provider + chuỗi kết nối). Có 2 nơi để cấu hình:

1. **`OnConfiguring()` trong DbContext**: lệnh scaffold mặc định sinh hàm này và ghi cứng chuỗi kết nối (kèm mật khẩu) vào code. Dùng được với `new Prn212PQlbtContext()`.
2. **Dependency Injection (DI)**: Server đăng ký DbContext trong `Asp/ServerHost.cs`, chuỗi kết nối đọc từ `appsettings.json`:

   ```csharp
   builder.Services.AddDbContext<Prn212PQlbtContext>(options => options.UseSqlServer(config.GetConnectionString("Default")));
   ```

   Khi một endpoint cần `Prn212PQlbtContext`, ASP.NET tạo context mới cho mỗi request và truyền cấu hình vào constructor có sẵn trong code scaffold:

   ```csharp
   public Prn212PQlbtContext(DbContextOptions<Prn212PQlbtContext> options) : base(options) { }
   ```

#### Tại sao cần `--no-onconfiguring`

- Không còn `OnConfiguring()` thì `new Prn212PQlbtContext()` (constructor không tham số) **không có provider**, truy vấn đầu tiên sẽ lỗi _"No database provider has been configured"_. Luôn lấy context từ DI.
- GUI quản trị (thao tác trực tiếp CSDL, không có DI) thì lấy context qua scope của `WebApplication`, hoặc tự tạo options:

  ```csharp
  DbContextOptions<Prn212PQlbtContext> options = new DbContextOptionsBuilder<Prn212PQlbtContext>()
      .UseSqlServer(connectionString)
      .Options;
  using Prn212PQlbtContext db = new Prn212PQlbtContext(options);
  ```

### Lệnh scaffold cũ (tham khảo)

```bash
dotnet ef dbcontext scaffold "Server=(local);uid=sa;password=<password>;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --force
```

### appsettings.json

Tạo `appsettings.json` (set `Copy to Output Directory = Always`):

```json
{
  "ConnectionStrings": {
    "Default": "Server=(local);uid=sa;password=<password>;database=PRN212_P_QLBT;Encrypt=True;TrustServerCertificate=True;"
  }
}
```
