# 02: อัปเกรดโมเดล Policy และข้อมูลเดิม

**What to build:** Policy มี Sum Insured, ชั้นประกัน, Premium, ช่วงคุ้มครอง 1 ปี และสถานะ Active/Expired/Cancelled (พร้อมวันที่และเหตุผลยกเลิก) Coverage Limit เท่ากับ Sum Insured เสมอ สคริปต์ migration เติมข้อมูลเดิม: Sum Insured = Coverage Limit เดิม ช่วงคุ้มครองครอบ Claim เดิมทั้งหมด สถานะ Active รันซ้ำได้ ไม่ทำให้ Claim เดิมพัง

**Blocked by:** Controller refactor (ADR 0005) ต้องเสร็จก่อน (เงื่อนไขภายนอก ไม่ใช่ ticket ในชุดนี้)

**Status:** ready-for-agent

- [ ] migration รันบน LocalDB ที่มีข้อมูลเฟสแรกจริง ตรวจจำนวนแถวก่อน/หลัง และรันซ้ำสองรอบไม่เปลี่ยนผล (ตรวจมือ)
- [ ] Claim เดิมยังเปิดดู ส่ง และเดินวงจรสถานะได้เหมือนเดิม
- [ ] API ค้นหา/ดู Policy คืนฟิลด์ใหม่ และสถานะ Expired คำนวณจากวันที่ ณ เวลาตรวจ ไม่ใช่งานเบื้องหลัง
- [ ] test ผ่าน seam HTTP ด้วยนาฬิกาจำลอง
