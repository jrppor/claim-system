---
status: accepted
---

# เก็บรูปความเสียหายเป็นไฟล์บนดิสก์ผ่าน `IFileStorage`

รูปของ Claim เก็บเป็นไฟล์ในโฟลเดอร์นอก wwwroot ที่ตั้งค่าได้ ผ่าน interface `IFileStorage` ตัวเดียว และเก็บเมทาดาทาในตาราง `ClaimPhotos` ทำให้สลับไป Azure Blob หรือ S3 ภายหลังได้โดยไม่แตะ Application และไม่ทำให้ฐานข้อมูลบวม รูปเสิร์ฟผ่าน endpoint ที่ตรวจสิทธิ์เท่านั้น ไม่มี URL สาธารณะ

## Considered Options

- `varbinary` ใน SQL Server: ง่ายกว่าและ backup พร้อม DB แต่ทำให้ DB บวมและไม่ตรงกับแนวทางขององค์กรใหญ่
