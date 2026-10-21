# 09: ซ่อม ออก Invoice และยืนยันจ่าย

**What to build:** Garage Staff เปลี่ยน Approved Claim เป็น InRepair และ Repaired ส่ง Invoice (InvoiceSubmitted) แล้ว Insurance Adjuster ดู Invoice เทียบกับ Approved Amount และยืนยันจ่ายจนเป็น Paid (Closed) พร้อม History และอีเมลทุกขั้น

**Blocked by:** 08

**Status:** ready-for-agent

- [ ] ทำได้เฉพาะตามลำดับสถานะและตามบทบาท
- [ ] Invoice แสดงเทียบกับ Approved Amount
- [ ] Claim ที่ Paid แก้ไม่ได้
- [ ] test ผ่าน seam HTTP ครอบ flow เต็มจาก Draft ถึง Paid
