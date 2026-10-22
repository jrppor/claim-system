# 09: รายการและค้นหา Quotation และ Policy

**What to build:** หน้ารายการ Quotation และ Policy พร้อมค้นหา (เลขที่ ทะเบียน Customer) กรองสถานะ แบ่งหน้า Underwriter ใช้ได้เต็ม Admin เข้าดูอย่างเดียว Garage Staff และ Adjuster เข้าไม่ได้ (Garage Staff ยังค้นหา Policy ที่ Active ตอนเปิด Claim ได้เหมือนเดิม)

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] ค้นหา กรอง แบ่งหน้าทำงานถูกต้องทั้ง Quotation และ Policy
- [ ] สถานะ Expired ของ Quotation/Policy แสดงตามวันที่ ณ เวลาตรวจ
- [ ] Admin อ่านได้แต่แก้ไม่ได้ (403); บทบาทอื่นได้ 403
- [ ] test ผ่าน seam HTTP; npm run build ผ่าน
