# 04: Thumbnail ของรูป

**What to build:** ตอนอัปโหลดสร้าง thumbnail ขนาดเดียว (ด้านยาวสุด 320 px) เก็บผ่าน `IFileStorage` gallery แสดง thumbnail และคลิกดูรูปเต็ม ลบรูปแล้วลบ thumbnail ด้วย

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] มี endpoint ดึง thumbnail ตรวจสิทธิ์และการมองเห็นเหมือนรูปเต็ม
- [ ] รูปที่เล็กกว่า 320 px ไม่ถูกขยาย
- [ ] ลบรูปแล้วทั้งต้นฉบับและ thumbnail ถูกลบ
- [ ] gallery ใช้ thumbnail และเปิดรูปเต็มเมื่อคลิก; ตรวจมือกับดิสก์จริงหนึ่งรอบ
- [ ] test ผ่าน seam HTTP
