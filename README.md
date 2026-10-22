# ระบบเคลมประกันรถยนต์ (Claim)

ระบบจัดการเคลมประกันรถยนต์ระหว่างอู่ซ่อม (Garage Staff) และบริษัทประกัน (Insurance Adjuster) พร้อมผู้ดูแลระบบ (Admin)
ประกอบด้วย `api/` (ASP.NET Core, SQL Server, Redis) และ `ui/` (Next.js)
คำศัพท์ดูที่ [GLOSSARY.md](GLOSSARY.md) และการตัดสินใจเชิงสถาปัตยกรรมที่ [docs/adr/](docs/adr/)

## สิ่งที่ต้องมี

- .NET SDK 10
- Node.js 20+ และ npm
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)
- Redis ที่ `localhost:6379` (เก็บ refresh token)
- SMTP mail catcher ที่พอร์ต SMTP `1025` และหน้าเว็บ `8025` (MailHog หรือ Mailpit ใช้แทนกันได้)

## สร้างฐานข้อมูล

```powershell
cd api/db
./Initialize-Database.ps1
```

สคริปต์สร้างฐานข้อมูล `Claim` (ถ้ายังไม่มี) แล้วรัน `NNN_*.sql` ตามลำดับ สคริปต์ทั้งหมดรันซ้ำได้ ไม่ทำให้ข้อมูลซ้ำ
รวมถึง `010_seed_demo_claims.sql` ที่สร้างเคลมตัวอย่าง `CLM-DEMO-001` ถึง `010` ในสถานะต่าง ๆ

## รัน API

```powershell
cd api
$env:Jwt__SigningKey = "<คีย์ลับอย่างน้อย 32 ตัวอักษร>"
dotnet run --project src/Claim.Api --urls http://localhost:5000
```

- `Jwt__SigningKey` จำเป็นเมื่อไม่ได้รันในโหมด Development (โหมด Development มีคีย์สำหรับพัฒนาใน `appsettings.Development.json`)
- ค่าอื่นแก้ได้ด้วย environment variable รูปแบบ `Section__Key` เช่น `ConnectionStrings__Claim`, `Redis__Configuration`, `Smtp__Host`, `Smtp__Port`, `Cors__AllowedOrigins__0`
- Swagger อยู่ที่ `/swagger`, health check ที่ `/health`

## รัน UI

```powershell
cd ui
copy .env.example .env.local   # NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
npm install
npm run dev                    # http://localhost:3000
```

## บัญชีตัวอย่าง

| บทบาท | อีเมล | รหัสผ่าน |
|---|---|---|
| Garage Staff | staff@claim.local | Garage123! |
| Insurance Adjuster | adjuster@claim.local | Adjuster123! |
| Admin | admin@claim.local | Admin123! |

อีเมลที่ระบบส่งถึงลูกค้าดูได้ที่หน้าเว็บของ mail catcher: http://localhost:8025

## กติกาสำคัญของ API

ทุกคำขอที่แก้ไขเคลมต้องส่งเวอร์ชันที่อ่านมาในหัว `If-Match` (ค่า `version` ใน JSON ของเคลม)
ไม่ส่ง = 428, ส่ง `*` ไม่ได้, เวอร์ชันเก่า = 409 (`type: urn:claim:version-conflict`)

## รันเทสต์

```powershell
cd api
dotnet test        # ทดสอบผ่าน HTTP ด้วย WebApplicationFactory และ fake ในหน่วยความจำ ไม่ต้องใช้ฐานข้อมูล
cd ../ui
npm run build
```

## Phase 2

รายการที่ตัดออกจากเฟสแรก (ดู [.scratch/phase-2-backlog.md](.scratch/phase-2-backlog.md)):

- อัปโหลดรูปความเสียหาย
- การคำนวณเบี้ยประกัน
- การออกกรมธรรม์ (เฟสแรกใช้ Policy ที่ seed ไว้)
- การชำระเงินจริง
- การเชื่อมระบบภายนอก / ระบบประกันจริง
- integration test ของ SQL/Dapper กับ SQL Server จริง
- Redis cache ข้อมูลอ้างอิง
