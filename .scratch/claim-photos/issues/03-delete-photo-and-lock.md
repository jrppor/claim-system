# 03: ลบรูปและล็อกรูปหลัง Approved

**What to build:** Garage Staff ลบรูปได้ในสถานะที่อนุญาต ไฟล์ถูกลบจริงจากที่เก็บ บันทึก Claim History และเมื่อ Claim เป็น Approved หรือสถานะหลังจากนั้นหรือ Cancelled/Rejected ทั้งอัปโหลดและลบถูกปฏิเสธ (ยังดูได้) UI ซ่อนปุ่มตามบทบาทและสถานะ

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] ลบรูปสำเร็จ รูปหายจากรายการ ไฟล์ถูกลบจากที่เก็บ
- [ ] ลบหรืออัปโหลดใน Approved, InRepair, Repaired, InvoiceSubmitted, Paid, Rejected, Cancelled ได้ 409
- [ ] Adjuster และ Admin ลบไม่ได้ (403) แต่ยังดูรูปได้
- [ ] ต้องมี If-Match และตรวจ version
- [ ] test ผ่าน seam HTTP ครบทุกสถานะ
