# 01: API walking skeleton

**What to build:** วางโครง API ให้รันและทดสอบได้ครบเส้น: solution 4 โปรเจค (Domain, Application, Infrastructure, Api) ตาม ADR 0003, สคริปต์ SQL ลำดับเลขและสคริปต์ PowerShell ที่สร้าง DB `Claim` บน LocalDB พร้อมตาราง Users (ADR 0004), Serilog, ProblemDetails, Swagger, endpoint health และ test host (`WebApplicationFactory` กับ fake ใน memory)

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] สคริปต์ PowerShell สร้าง DB `Claim` และรันสคริปต์ SQL ตามลำดับได้บนเครื่องที่สะอาด
- [ ] API รันได้ Swagger เปิดได้ และ error ตอบเป็น ProblemDetails
- [ ] มี test ที่เรียก health ผ่าน test host ที่ใช้ fake แล้วผ่าน
- [ ] ค่า config (connection string, Redis `localhost:6379`, SMTP `localhost:1025`, ชื่ออู่และบริษัทประกัน) แก้ได้ภายนอกโค้ด
