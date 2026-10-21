# DB-first ด้วยสคริปต์ SQL และ Dapper ไม่ใช้ EF migrations

schema เป็นต้นทาง (source of truth) เขียนเป็นสคริปต์ SQL ลำดับเลข `api/db/NNN_*.sql` และมีสคริปต์ PowerShell สร้าง DB `Claim` บน LocalDB แล้วรันตามลำดับ โค้ดเข้าถึงข้อมูลด้วย Dapper ไม่ใช้ EF Core ตามที่เจ้าของโปรเจคกำหนด และให้ใช้ความสามารถเฉพาะของ SQL Server ได้ เช่น `rowversion` และ `SEQUENCE`

## Consequences

ผูกกับ SQL Server การเปลี่ยนไปใช้ DB อื่นต้องเขียน SQL ใหม่ นอกจากนี้ SQL ของ Dapper ไม่ถูกทดสอบอัตโนมัติในเฟสแรก เพราะ integration test ใช้ fake repository (ดู `.scratch/phase-2-backlog.md`)
